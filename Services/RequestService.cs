using TelegramBot.Constants;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class RequestService
    {
        public static string? LastCreateError { get; private set; }

        private static readonly string[] StatusInsertCandidates =
        {
            RequestStatus.Pending,
            "new",
            "created",
            "PENDING"
        };

        public static async Task<List<Request>> GetApprovedRequestsAsync()
        {
            var response = await SupabaseConfig.GetClient().From<Request>().Get();
            return (response.Models ?? new List<Request>())
                .Where(r => IsApprovedStatus(r.Status))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static async Task<List<Request>> GetPendingRequestsAsync()
        {
            var response = await SupabaseConfig.GetClient().From<Request>().Get();
            return (response.Models ?? new List<Request>())
                .Where(r => IsPendingStatus(r.Status))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static async Task<List<Request>> GetByClientTelegramIdAsync(long telegramId)
        {
            var client = await ClientService.GetByTelegramIdAsync(telegramId);
            if (client == null) return new List<Request>();

            var res = await SupabaseConfig.GetClient().From<Request>().Where(r => r.ClientId == client.ClientId).Get();
            return (res.Models ?? new List<Request>())
                .Where(r => IsPendingStatus(r.Status) || IsApprovedStatus(r.Status))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static async Task<Request?> GetByIdAsync(Guid requestId)
        {
            var res = await SupabaseConfig.GetClient().From<Request>().Where(r => r.RequestId == requestId).Get();
            return res.Models?.FirstOrDefault();
        }

        public static async Task<bool> UpdateRequestStatusAsync(Guid requestId, string newStatus, string? comment = null)
        {
            try
            {
                var status = NormalizeStatus(newStatus);
                if (comment != null)
                {
                    var res = await SupabaseConfig.GetClient()
                        .From<Request>()
                        .Where(r => r.RequestId == requestId)
                        .Set(r => r.Status, status)
                        .Set(r => r.Comment, comment)
                        .Update();
                    return res.Models?.Count > 0;
                }

                var response = await SupabaseConfig.GetClient()
                    .From<Request>()
                    .Where(r => r.RequestId == requestId)
                    .Set(r => r.Status, status)
                    .Update();
                return response.Models?.Count > 0;
            }
            catch { return false; }
        }

        public static async Task<Guid?> CreateRequestAsync(
            long telegramId,
            string username,
            string firstName,
            DateOnly? desiredDate,
            TimeOnly? desiredTime,
            List<Guid> serviceIds,
            string contactInfo,
            Guid? workingDateId = null,
            Guid? timeSlotId = null)
        {
            LastCreateError = null;
            try
            {
                if (serviceIds.Count == 0)
                {
                    LastCreateError = "Не выбраны услуги";
                    return null;
                }

                var client = await ClientService.GetOrCreateAsync(telegramId, firstName, username);
                var requestId = Guid.NewGuid();

                Request? createdRequest = null;
                Exception? lastEx = null;

                foreach (var status in StatusInsertCandidates)
                {
                    try
                    {
                        var newRequest = new Request
                        {
                            RequestId = requestId,
                            ClientId = client.ClientId,
                            DesiredDate = desiredDate,
                            DesiredTime = desiredTime,
                            Comment = contactInfo ?? "",
                            Status = NormalizeStatus(status),
                            CreatedAt = DateTime.UtcNow
                        };

                        var requestResponse = await SupabaseConfig.GetClient().From<Request>().Insert(newRequest);
                        createdRequest = requestResponse.Models?.FirstOrDefault();
                        if (createdRequest != null)
                            break;
                    }
                    catch (Exception ex) when (IsStatusConstraintError(ex))
                    {
                        lastEx = ex;
                        Console.WriteLine($"CreateRequest status '{status}' rejected: {ex.Message}");
                    }
                }

                if (createdRequest == null)
                {
                    LastCreateError = lastEx != null
                        ? $"{lastEx.Message}\n\nВ Supabase SQL Editor выполните supabase_fix_requests_status.sql (удалить CHECK) и Redeploy на Render."
                        : "Не удалось сохранить заявку. Выполните supabase_fix_requests_status.sql";
                    return null;
                }

                foreach (var serviceId in serviceIds)
                {
                    var item = new RequestItem
                    {
                        RequestId = createdRequest.RequestId,
                        ServiceId = serviceId,
                        Quantity = 1
                    };
                    await SupabaseConfig.GetClient().From<RequestItem>().Insert(item);
                }

                if (timeSlotId.HasValue)
                {
                    try
                    {
                        await SupabaseConfig.GetClient()
                            .From<TimeSlot>()
                            .Where(s => s.TimeSlotId == timeSlotId.Value)
                            .Set(s => s.IsBooked, true)
                            .Update();
                    }
                    catch (Exception slotEx)
                    {
                        Console.WriteLine($"TimeSlot mark booked warning: {slotEx.Message}");
                    }
                }

                return createdRequest.RequestId;
            }
            catch (Exception ex)
            {
                LastCreateError = ex.Message;
                Console.WriteLine($"CreateRequest error: {ex}");
                return null;
            }
        }

        public static async Task<bool> DeleteRequestAsync(Guid requestId)
        {
            await SupabaseConfig.GetClient().From<RequestItem>().Where(ri => ri.RequestId == requestId).Delete();
            await SupabaseConfig.GetClient().From<Request>().Where(r => r.RequestId == requestId).Delete();
            return true;
        }

        public static async Task<string> FormatRequestSummaryAsync(Request request, IEnumerable<Guid> serviceIds)
        {
            var services = await CatalogService.FormatServiceListAsync(serviceIds);
            var date = request.DesiredDate?.ToString("dd.MM.yyyy") ?? "—";
            var time = request.DesiredTime?.ToString("HH:mm") ?? "—";
            return $"Услуги: {services}.\nЗапись на дату {date} время {time}.\nДанные для связи: {request.Comment}";
        }

        private static string NormalizeStatus(string status) => status.Trim().ToLowerInvariant();

        private static bool IsStatusConstraintError(Exception ex) =>
            ex.Message.Contains("23514", StringComparison.Ordinal) ||
            ex.Message.Contains("requests_status_check", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("check constraint", StringComparison.OrdinalIgnoreCase);

        private static bool IsPendingStatus(string? status) =>
            string.Equals(NormalizeStatus(status ?? ""), RequestStatus.Pending, StringComparison.Ordinal)
            || string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "new", StringComparison.OrdinalIgnoreCase);

        private static bool IsApprovedStatus(string? status) =>
            string.Equals(NormalizeStatus(status ?? ""), RequestStatus.Approved, StringComparison.Ordinal)
            || string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
    }
}

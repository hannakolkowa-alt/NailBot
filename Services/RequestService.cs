using TelegramBot.Constants;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class RequestService
    {
        public static async Task<List<Request>> GetPendingRequestsAsync()
        {
            var response = await SupabaseConfig.Client.From<Request>().Get();
            return (response.Models ?? new List<Request>())
                .Where(r => r.Status == RequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static async Task<List<Request>> GetByClientTelegramIdAsync(long telegramId)
        {
            var client = await ClientService.GetByTelegramIdAsync(telegramId);
            if (client == null) return new List<Request>();

            var res = await SupabaseConfig.Client.From<Request>().Where(r => r.ClientId == client.ClientId).Get();
            return (res.Models ?? new List<Request>())
                .Where(r => r.Status is RequestStatus.Pending or RequestStatus.Approved)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static async Task<Request?> GetByIdAsync(Guid requestId)
        {
            var res = await SupabaseConfig.Client.From<Request>().Where(r => r.RequestId == requestId).Get();
            return res.Models?.FirstOrDefault();
        }

        public static async Task<bool> UpdateRequestStatusAsync(Guid requestId, string newStatus, string? comment = null)
        {
            try
            {
                if (comment != null)
                {
                    var res = await SupabaseConfig.Client
                        .From<Request>()
                        .Where(r => r.RequestId == requestId)
                        .Set(r => r.Status, newStatus)
                        .Set(r => r.Comment, comment)
                        .Update();
                    return res.Models?.Count > 0;
                }

                var response = await SupabaseConfig.Client
                    .From<Request>()
                    .Where(r => r.RequestId == requestId)
                    .Set(r => r.Status, newStatus)
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
            try
            {
                var client = await ClientService.GetOrCreateAsync(telegramId, firstName, username);

                var newRequest = new Request
                {
                    RequestId = Guid.NewGuid(),
                    ClientId = client.ClientId,
                    DesiredDate = desiredDate,
                    DesiredTime = desiredTime,
                    Comment = contactInfo,
                    Status = RequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var requestResponse = await SupabaseConfig.Client.From<Request>().Insert(newRequest);
                var createdRequest = requestResponse.Models?.FirstOrDefault();
                if (createdRequest == null) return null;

                foreach (var serviceId in serviceIds)
                {
                    var item = new RequestItem { RequestId = createdRequest.RequestId, ServiceId = serviceId, Quantity = 1 };
                    await SupabaseConfig.Client.From<RequestItem>().Insert(item);
                }

                if (timeSlotId.HasValue)
                {
                    await SupabaseConfig.Client
                        .From<TimeSlot>()
                        .Where(s => s.TimeSlotId == timeSlotId.Value)
                        .Set(s => s.IsBooked, true)
                        .Update();
                }

                return createdRequest.RequestId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateRequest error: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> DeleteRequestAsync(Guid requestId)
        {
            await SupabaseConfig.Client.From<RequestItem>().Where(ri => ri.RequestId == requestId).Delete();
            await SupabaseConfig.Client.From<Request>().Where(r => r.RequestId == requestId).Delete();
            return true;
        }

        public static async Task<string> FormatRequestSummaryAsync(Request request, IEnumerable<Guid> serviceIds)
        {
            var services = await CatalogService.FormatServiceListAsync(serviceIds);
            var date = request.DesiredDate?.ToString("dd.MM.yyyy") ?? "—";
            var time = request.DesiredTime?.ToString("HH:mm") ?? "—";
            return $"Услуги: {services}.\nЗапись на дату {date} время {time}.\nДанные для связи: {request.Comment}";
        }
    }
}

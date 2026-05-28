using TelegramBot.Models;

namespace TelegramBot.Services
{
    /// <summary>
    /// Сервис для управления бизнес-логикой заявок.
    /// Содержит методы для создания новых запросов на запись, изменения их статусов и выборки из БД.
    /// </summary>
    public static class RequestService
    {
        public static async Task<List<Request>> GetNewRequestsAsync()
        {
            var response = await SupabaseConfig.Client
                .From<Request>()
                .Where(r => r.Status == "APPROVED")
                .Order(r => r.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();
            return response.Models ?? new List<Request>();
        }

        public static async Task<bool> UpdateRequestStatusAsync(Guid requestId, string newStatus)
        {
            try
            {
                var response = await SupabaseConfig.Client
                    .From<Request>()
                    .Where(r => r.RequestId == requestId)
                    .Set(r => r.Status, newStatus)
                    .Update();
                return response.Models.Count > 0;
            }
            catch { return false; }
        }

        public static async Task<Guid?> CreateRequestAsync(long telegramId, string username, string firstName, DateOnly? desiredDate, TimeOnly? desiredTime, List<Guid> serviceIds, string comment = null)
        {
            try
            {
                var clientResponse = await SupabaseConfig.Client.From<Client>().Where(c => c.TelegramId == telegramId).Single();
                Client client = clientResponse ?? new Client { TelegramId = telegramId, FirstName = firstName, TelegramUsername = username };

                if (clientResponse == null)
                {
                    var insertClient = await SupabaseConfig.Client.From<Client>().Insert(client);
                    client = insertClient.Models.First();
                }

                var newRequest = new Request { ClientId = client.ClientId, DesiredDate = desiredDate, DesiredTime = desiredTime, Comment = comment, Status = "APPROVED" };
                var requestResponse = await SupabaseConfig.Client.From<Request>().Insert(newRequest);
                var createdRequest = requestResponse.Models.FirstOrDefault();

                if (createdRequest == null) return null;

                foreach (var serviceId in serviceIds)
                {
                    var item = new RequestItem { RequestId = createdRequest.RequestId, ServiceId = serviceId, Quantity = 1 };
                    await SupabaseConfig.Client.From<RequestItem>().Insert(item);
                }

                return createdRequest.RequestId;
            }
            catch { return null; }
        }
    }
}
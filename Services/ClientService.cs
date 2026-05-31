using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ClientService
    {
        public static async Task<Client?> GetByTelegramIdAsync(long telegramId)
        {
            var res = await SupabaseConfig.Client.From<Client>().Where(c => c.TelegramId == telegramId).Get();
            return res.Models?.FirstOrDefault();
        }

        public static async Task<Client> GetOrCreateAsync(long telegramId, string? firstName, string? username)
        {
            var existing = await GetByTelegramIdAsync(telegramId);
            if (existing != null) return existing;

            var client = new Client
            {
                ClientId = Guid.NewGuid(),
                TelegramId = telegramId,
                FirstName = firstName ?? "Клиент",
                TelegramUsername = username ?? ""
            };
            var res = await SupabaseConfig.Client.From<Client>().Insert(client);
            return res.Models!.First();
        }

        public static async Task<List<Client>> GetAllClientsAsync()
        {
            var res = await SupabaseConfig.Client.From<Client>().Get();
            return res.Models ?? new List<Client>();
        }

        public static async Task<bool> DeleteClientAsync(Guid clientId)
        {
            await SupabaseConfig.Client.From<Client>().Where(c => c.ClientId == clientId).Delete();
            return true;
        }

        public static async Task ClearAllClientsAsync()
        {
            var all = await GetAllClientsAsync();
            foreach (var c in all)
                await DeleteClientAsync(c.ClientId);
        }
    }
}

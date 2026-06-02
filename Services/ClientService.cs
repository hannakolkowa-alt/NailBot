using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ClientService
    {
        public static async Task<Client?> GetByTelegramIdAsync(long telegramId)
        {
            var res = await SupabaseConfig.GetClient().From<Client>().Where(c => c.TelegramId == telegramId).Get();
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
                TelegramUsername = (username ?? "").Trim().TrimStart('@')
            };
            var res = await SupabaseConfig.GetClient().From<Client>().Insert(client);
            var created = res.Models?.FirstOrDefault();
            if (created != null)
                return created;

            existing = await GetByTelegramIdAsync(telegramId);
            if (existing != null)
                return existing;

            throw new InvalidOperationException("Не удалось создать клиента (таблица clients). Выполните supabase_booking_tables.sql");
        }

        public static async Task<List<Client>> GetAllClientsAsync()
        {
            var res = await SupabaseConfig.GetClient().From<Client>().Get();
            return res.Models ?? new List<Client>();
        }

        public static async Task<bool> DeleteClientAsync(Guid clientId)
        {
            await SupabaseConfig.GetClient().From<Client>().Where(c => c.ClientId == clientId).Delete();
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

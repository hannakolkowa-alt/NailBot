using TelegramBot.Models;

namespace TelegramBot.Services
{
    /// <summary>
    /// Сервис для работы с информацией о мастерах.
    /// Обеспечивает получение данных профиля, квалификации и описания мастера из базы данных.
    /// </summary>
    public static class MasterService
    {
        public static async Task<Master?> GetMasterProfileAsync()
        {
            try
            {
                var response = await SupabaseConfig.GetClient().From<Master>().Get();
                return response.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetMasterProfile: {ex.Message}");
                throw;
            }
        }

        /// <summary>Создаёт профиль мастера, если в БД ещё нет записи (нужно для расписания).</summary>
        public static async Task<Master> EnsureMasterExistsAsync(string name, string? telegramUsername)
        {
            var existing = await GetMasterProfileAsync();
            if (existing != null)
                return existing;

            var masterId = Guid.NewGuid();
            var saved = await SaveOrUpdateProfileAsync(
                masterId,
                string.IsNullOrWhiteSpace(name) ? "Мастер" : name.Trim(),
                telegramUsername?.Trim().TrimStart('@') ?? "master",
                "—",
                "Профиль создан автоматически. Измените в «Мой профиль».");

            if (!saved)
                throw new InvalidOperationException("Не удалось создать профиль в таблице masters. Проверьте таблицу в Supabase.");

            var created = await GetMasterProfileAsync();
            if (created == null)
                throw new InvalidOperationException("Профиль мастера не найден после сохранения.");

            return created;
        }

        /// <summary>
        /// Сохраняет или обновляет профиль мастера в базе данных (автоматический Upsert).
        /// </summary>
        public static async Task<bool> SaveOrUpdateProfileAsync(Guid masterId, string name, string username, string experience, string description)
        {
            var master = new Master
            {
                MasterId = masterId,
                Name = name,
                TelegramUsername = string.IsNullOrWhiteSpace(username) ? "master" : username,
                Experience = experience,
                Description = description
            };

            var existing = await GetMasterProfileAsync();
            if (existing == null)
            {
                var inserted = await SupabaseConfig.GetClient().From<Master>().Insert(master);
                return inserted.Models?.Count > 0;
            }

            master.UpdatedAt = DateTime.UtcNow;
            var updated = await SupabaseConfig.GetClient()
                .From<Master>()
                .Where(m => m.MasterId == master.MasterId)
                .Update(master);
            return updated.Models?.Count > 0;
        }
    }
}

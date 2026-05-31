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
            var response = await SupabaseConfig.GetClient().From<Master>().Get();
            return response.Models?.FirstOrDefault();
        }

        /// <summary>Создаёт профиль мастера, если в БД ещё нет записи (нужно для расписания).</summary>
        public static async Task<Master> EnsureMasterExistsAsync(string name, string? telegramUsername)
        {
            var existing = await GetMasterProfileAsync();
            if (existing != null)
                return existing;

            var masterId = Guid.NewGuid();
            await SaveOrUpdateProfileAsync(
                masterId,
                string.IsNullOrWhiteSpace(name) ? "Мастер" : name.Trim(),
                telegramUsername?.Trim().TrimStart('@') ?? "",
                "—",
                "Профиль создан автоматически. Измените в «Мой профиль».");

            return (await GetMasterProfileAsync())!;
        }

        /// <summary>
        /// Сохраняет или обновляет профиль мастера в базе данных (автоматический Upsert).
        /// </summary>
        public static async Task<bool> SaveOrUpdateProfileAsync(Guid masterId, string name, string username, string experience, string description)
        {
            try
            {
                var master = new Master
                {
                    MasterId = masterId,
                    Name = name,
                    TelegramUsername = username,
                    Experience = experience,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow
                };

                // Upsert автоматически обновит запись, если master_id совпадет, или создаст новую
                var response = await SupabaseConfig.GetClient().From<Master>().Upsert(master);
                return response.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения профиля: {ex.Message}");
                return false;
            }
        }
    }
}

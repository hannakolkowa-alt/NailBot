using Telegram.Bot;
using TelegramBot.UI;

namespace TelegramBot.Helpers
{
    public static class BotMessenger
    {
        public static async Task NotifyErrorAsync(ITelegramBotClient bot, long chatId, Exception ex, CancellationToken ct)
        {
            Console.WriteLine($"User {chatId} error: {ex}");
            try
            {
                var msg = ex.Message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
                    ? "⚠️ Нет прав к таблицам в Supabase.\n\n1) На Render в SupabaseKey укажите secret (sb_secret_) или service_role (eyJ...), не publishable.\n2) В Supabase → SQL Editor выполните файл supabase_permissions.sql из проекта."
                    : ex.Message.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase)
                    ? "⚠️ Неверный ключ Supabase.\n\nНа Render задайте:\n• SupabaseUrl — https://ВАШ-проект.supabase.co\n• SupabaseKey — secret (sb_secret_...) или service_role (eyJ...), НЕ publishable."
                    : ex.Message.Contains("masters", StringComparison.OrdinalIgnoreCase)
                    ? "⚠️ Таблица masters не совпадает с ботом.\n\n1) SQL Editor → выполните supabase_fix_masters.sql\n2) Settings → API → Reload schema\n3) Redeploy на Render → /master"
                    : ex.Message.Contains("working_dates", StringComparison.OrdinalIgnoreCase) ||
                      ex.Message.Contains("time_slots", StringComparison.OrdinalIgnoreCase) ||
                      ex.Message.Contains("column \"time\"", StringComparison.OrdinalIgnoreCase) ||
                      ex.Message.Contains("время", StringComparison.OrdinalIgnoreCase)
                    ? "⚠️ Ошибка таблицы time_slots.\nВ Supabase SQL Editor выполните supabase_fix_time_slots.sql"
                    : $"⚠️ {Truncate(ex.Message, 350)}";

                await bot.SendMessage(chatId, msg, replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
            }
            catch { }
        }

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text[..max] + "…";
    }
}

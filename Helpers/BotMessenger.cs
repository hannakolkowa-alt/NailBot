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
                var msg = ex.Message.Contains("Supabase", StringComparison.OrdinalIgnoreCase) ||
                          ex is NullReferenceException
                    ? "⚠️ Ошибка базы данных. Проверьте SupabaseUrl/SupabaseKey на Render."
                    : $"⚠️ Ошибка: {ex.Message}";

                await bot.SendMessage(chatId, msg, replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
            }
            catch { }
        }
    }
}

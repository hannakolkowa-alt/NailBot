using Telegram.Bot;
using TelegramBot.UI;

namespace TelegramBot.Helpers
{
    public static class ClientMenuFooter
    {
        public static async Task SendAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
        {
            await bot.SendMessage(chatId, "Клиентское меню 👇",
                replyMarkup: Keyboards.GetMenuForUser(chatId, userId),
                cancellationToken: ct);
        }
    }
}

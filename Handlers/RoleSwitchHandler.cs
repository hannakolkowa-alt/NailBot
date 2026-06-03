using Telegram.Bot;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class RoleSwitchHandler
    {
        public static async Task<bool> TryHandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, CancellationToken ct)
        {
            if (!RoleHelper.IsMasterAccount(userId))
                return false;

            var normalized = MenuTexts.Normalize(text);

            if (normalized == "/client")
            {
                RoleHelper.SetClientMode(chatId);
                SessionStore.Reset(chatId);
                await bot.SendMessage(chatId,
                    "Клиентское меню 👇",
                    replyMarkup: Keyboards.CreateMainMenuKeyboard(),
                    cancellationToken: ct);
                return true;
            }

            if (normalized is "/master" or "/admin")
            {
                RoleHelper.SetMasterMode(chatId);
                SessionStore.Reset(chatId);
                await MasterService.EnsureMasterExistsAsync("Мастер", null);
                await bot.SendMessage(chatId,
                    "Меню мастера 👇",
                    replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                    cancellationToken: ct);
                return true;
            }

            return false;
        }
    }
}

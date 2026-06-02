using Telegram.Bot;
using TelegramBot.Helpers;
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

            if (MenuTexts.MasterToClientSwitch.Contains(normalized) || normalized == "/client")
            {
                RoleHelper.SetClientMode(chatId);
                SessionStore.Reset(chatId);
                await bot.SendMessage(chatId,
                    "🧪 Режим клиента (тест).\nЗапись, услуги, расписание — как у обычного клиента.\n\nВернуться: /master или кнопка «👩‍🎨 Мастер»",
                    replyMarkup: Keyboards.CreateMainMenuKeyboard(showMasterSwitch: true),
                    cancellationToken: ct);
                return true;
            }

            if (MenuTexts.ClientToMasterSwitch.Contains(normalized) || normalized is "/master" or "/admin")
            {
                RoleHelper.SetMasterMode(chatId);
                SessionStore.Reset(chatId);
                await MasterService.EnsureMasterExistsAsync("Мастер", null);
                await bot.SendMessage(chatId,
                    "👩‍🎨 Режим мастера.\n\n/client или «🧪 Клиент» — снова тест как клиент.",
                    replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                    cancellationToken: ct);
                return true;
            }

            return false;
        }
    }
}

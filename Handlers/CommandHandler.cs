using Telegram.Bot;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class CommandHandler
    {
        public static async Task HandleAsync(
            ITelegramBotClient botClient,
            long chatId,
            long userId,
            string command,
            string? firstName,
            string? username,
            CancellationToken ct)
        {
            if (command is "/client" or "/master" or "/admin")
            {
                if (await RoleSwitchHandler.TryHandleAsync(botClient, chatId, userId, command, ct))
                    return;
            }

            var actAsMaster = RoleHelper.ActAsMaster(chatId, userId);
            var keyboard = actAsMaster
                ? Keyboards.CreateAdminMenuKeyboard()
                : Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(userId));

            switch (command)
            {
                case "/start":
                    var greet = actAsMaster
                        ? "Привет! 👩‍🎨 Режим мастера.\n🧪 Клиент — тест как клиент\n/client — то же"
                        : RoleHelper.IsMasterAccount(userId)
                            ? "Привет! 🧪 Режим клиента (тест).\n/master — панель мастера"
                            : "Привет! Добро пожаловать в Nails Studio 💅";
                    await botClient.SendMessage(chatId, greet, replyMarkup: keyboard, cancellationToken: ct);
                    break;

                case "/menu":
                    var menuText = actAsMaster
                        ? "Меню мастера 👇"
                        : "Клиентское меню 👇";
                    await botClient.SendMessage(chatId, menuText, replyMarkup: keyboard, cancellationToken: ct);
                    break;

                case "/myid":
                    await botClient.SendMessage(chatId,
                        $"Ваш Telegram ID: {userId}\nРоль сейчас: {RoleHelper.RoleLabel(chatId, userId)}\n\n/master — мастер | /client — тест клиента",
                        cancellationToken: ct);
                    break;

                default:
                    if (command.StartsWith('/'))
                        await botClient.SendMessage(chatId, "Команды: /start /menu /master /client", replyMarkup: keyboard, cancellationToken: ct);
                    break;
            }
        }
    }
}

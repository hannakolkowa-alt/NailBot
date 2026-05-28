using Telegram.Bot;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    /// <summary>
    /// Обработчик базовых системных команд бота.
    /// Отвечает за реакцию на команды, начинающиеся со слэша (например, /start, /menu, /admin),
    /// и первичную навигацию пользователя по интерфейсу.
    /// </summary>
    public static class CommandHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, string command, bool isAdmin, CancellationToken ct)
        {
            var keyboard = isAdmin ? Keyboards.CreateAdminMenuKeyboard() : Keyboards.CreateMainMenuKeyboard();

            switch (command)
            {
                case "/start":
                    await botClient.SendMessage(chatId, "Привет Аня", replyMarkup: keyboard, cancellationToken: ct);
                    break;

                case "/menu":
                    string menuText = isAdmin
                        ? "Привет! ✨\n\nАдмин-панель открыта:"
                        : "Добро пожаловать в Nails Studio! 💅✨\n\nВыберите пункт меню:";
                    await botClient.SendMessage(chatId, menuText, replyMarkup: keyboard, cancellationToken: ct);
                    break;

                case "/admin":
                    if (isAdmin)
                        await botClient.SendMessage(chatId, "Админ-панель", replyMarkup: keyboard, cancellationToken: ct);
                    else
                        await botClient.SendMessage(chatId, "Доступ запрещен.", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                    break;

                default:
                    if (command.StartsWith('/'))
                        await botClient.SendMessage(chatId, "Неизвестная команда. Нажмите /start", replyMarkup: keyboard, cancellationToken: ct);
                    break;
            }
        }
    }
}
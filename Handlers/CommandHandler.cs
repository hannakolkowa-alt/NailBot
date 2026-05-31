using Telegram.Bot;
using TelegramBot.Services;
using TelegramBot.State;
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
        public static async Task HandleAsync(
            ITelegramBotClient botClient,
            long chatId,
            string command,
            bool isAdmin,
            string? firstName,
            string? username,
            CancellationToken ct)
        {
            var keyboard = isAdmin ? Keyboards.CreateAdminMenuKeyboard() : Keyboards.CreateMainMenuKeyboard();

            SessionStore.Reset(chatId);

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

                case "/master":
                case "/admin":
                    if (!isAdmin)
                    {
                        await botClient.SendMessage(chatId, "Доступ только для мастера.", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                        break;
                    }

                    await MasterService.EnsureMasterExistsAsync(firstName ?? "Мастер", username);
                    var session = SessionStore.GetOrCreate(chatId);
                    session.State = SessionState.Admin_Schedule_Date;

                    await botClient.SendMessage(chatId,
                        "👩‍🎨 Панель мастера\n\n" +
                        "📅 Добавление расписания:\n" +
                        "1) Введите дату: ГГГГ-ММ-ДД или ДД.ММ.ГГГГ\n" +
                        "2) Введите время: 10:00, 12:30 …\n" +
                        "3) Когда закончите — напишите «готово»\n\n" +
                        "Или выберите пункт в меню ниже.",
                        replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                        cancellationToken: ct);
                    break;

                default:
                    if (command.StartsWith('/'))
                        await botClient.SendMessage(chatId, "Неизвестная команда. Нажмите /start", replyMarkup: keyboard, cancellationToken: ct);
                    break;
            }
        }
    }
}
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    /// <summary>
    /// Обработчик пользовательского интерфейса.
    /// Отвечает за взаимодействие с обычными клиентами: просмотр информации о мастере,
    /// прайс-листа и запуск процесса записи на услуги.
    /// </summary>
    public static class ClientHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, string text, CancellationToken ct)
        {
            var kb = Keyboards.CreateMainMenuKeyboard();

            switch (text)
            {
                case "о мастере":
                    await botClient.SendMessage(chatId, "✨ О мастере:\nОпыт 5+ лет\nРаботаю в центре", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "галерея":
                    await botClient.SendMessage(chatId, "🖼️ Галерея работ (скоро)", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "прайс":
                    await botClient.SendMessage(chatId, "💰 Маникюр — 1500 ₽\nНаращивание — 2500 ₽", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "график":
                    await botClient.SendMessage(chatId, "📅 Пн–Пт 10:00–20:00", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "записаться":
                    await botClient.SendMessage(chatId, "📝 Напишите дату и время", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "мои записи":
                    await botClient.SendMessage(chatId, "📋 Нет активных записей", replyMarkup: kb, cancellationToken: ct);
                    break;
                default:
                    await botClient.SendMessage(chatId, "Выберите пункт меню 👇", replyMarkup: kb, cancellationToken: ct);
                    break;
            }
        }
    }
}
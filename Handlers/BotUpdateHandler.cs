using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Handlers
{
    /// <summary>
    /// Центральный узел распределения входящих обновлений.
    /// Определяет тип сообщения, роль пользователя (Админ/Клиент) 
    /// и направляет запрос в соответствующий специализированный обработчик.
    /// </summary>
    public static class BotUpdateHandler
    {
        private const long ADMIN_USER_ID = 5783971965;

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message || message.Text is not { } text) return;

            long chatId = message.Chat.Id;
            bool isAdmin = message.From?.Id == ADMIN_USER_ID;
            string command = NormalizeCommand(text);

            Console.WriteLine($"Получено от {chatId}: {text}");

            // 1. Проверяем базовые команды (старт, вызов меню)
            if (command.StartsWith("/"))
            {
                await CommandHandler.HandleAsync(botClient, chatId, command, isAdmin, ct);
                return;
            }

            // 2. Роутинг по тексту кнопок
            if (isAdmin)
            {
                await AdminHandler.HandleAsync(botClient, chatId, command, ct);
            }
            else
            {
                await ClientHandler.HandleAsync(botClient, chatId, command, ct);
            }
        }

        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Ошибка Telegram API:\n{exception.Message}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// /start и /start@BotName и /start параметр → /start
        /// </summary>
        private static string NormalizeCommand(string text)
        {
            var part = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            var atIndex = part.IndexOf('@');
            return atIndex > 0 ? part[..atIndex] : part;
        }
    }
}
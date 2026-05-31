using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot;

namespace TelegramBot.Handlers
{
    public static class BotUpdateHandler
    {
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery is { } callback)
            {
                await CallbackHandler.HandleAsync(botClient, callback, ct);
                return;
            }

            if (update.Message is not { } message) return;

            long chatId = message.Chat.Id;
            long userId = message.From?.Id ?? chatId;
            bool isAdmin = userId == BotConfig.AdminTelegramId;

            if (message.Text is { } text)
            {
                string command = NormalizeCommand(text);
                Console.WriteLine($"Получено от {chatId}: {text}");

                if (command.StartsWith("/"))
                {
                    await CommandHandler.HandleAsync(botClient, chatId, command, isAdmin, ct);
                    return;
                }

                if (await SessionInputHandler.TryHandleAsync(botClient, chatId, userId, text, isAdmin, ct))
                    return;

                if (isAdmin)
                    await AdminHandler.HandleAsync(botClient, chatId, text.ToLowerInvariant(), ct);
                else
                    await ClientHandler.HandleAsync(botClient, chatId, userId, text.ToLowerInvariant(), ct);
                return;
            }
        }

        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Ошибка Telegram API:\n{exception.Message}");
            return Task.CompletedTask;
        }

        private static string NormalizeCommand(string text)
        {
            var part = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            var atIndex = part.IndexOf('@');
            return atIndex > 0 ? part[..atIndex] : part;
        }
    }
}

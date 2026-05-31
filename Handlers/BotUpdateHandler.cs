using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class BotUpdateHandler
    {
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            try
            {
                if (update.CallbackQuery is { } callback)
                {
                    await CallbackHandler.HandleAsync(botClient, callback, ct);
                    return;
                }

                if (update.Message is not { } message || message.Text is not { } text)
                    return;

                long chatId = message.Chat.Id;
                long userId = message.From?.Id ?? chatId;
                bool isAdmin = userId == BotConfig.AdminTelegramId;
                var normalized = MenuTexts.Normalize(text);

                Console.WriteLine($"Получено от {chatId}: {text}");

                if (normalized.StartsWith('/'))
                {
                    SessionStore.Reset(chatId);
                    await CommandHandler.HandleAsync(botClient, chatId, NormalizeCommand(text), isAdmin, ct);
                    return;
                }

                if (MenuTexts.IsMenuButton(normalized))
                {
                    SessionStore.Reset(chatId);
                    if (isAdmin)
                        await AdminHandler.HandleAsync(botClient, chatId, normalized, ct);
                    else
                        await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
                    return;
                }

                if (await SessionInputHandler.TryHandleAsync(botClient, chatId, userId, text, isAdmin, ct))
                    return;

                if (isAdmin)
                    await AdminHandler.HandleAsync(botClient, chatId, normalized, ct);
                else
                    await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleUpdate error: {ex}");
                await HandleErrorAsync(botClient, ex, ct);
            }
        }

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Ошибка Telegram API:\n{exception}");
        }

        private static string NormalizeCommand(string text)
        {
            var part = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            var atIndex = part.IndexOf('@');
            return atIndex > 0 ? part[..atIndex] : part;
        }
    }
}

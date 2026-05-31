using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot;
using TelegramBot.Helpers;
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
                    if (MenuTexts.IsClientOnlyButton(normalized))
                    {
                        await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
                        return;
                    }

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
                var chatId = update.CallbackQuery?.Message?.Chat.Id
                    ?? update.Message?.Chat.Id;
                if (chatId.HasValue)
                    await BotMessenger.NotifyErrorAsync(botClient, chatId.Value, ex, ct);
            }
        }

        public static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Polling error: {exception}");
            return Task.CompletedTask;
        }

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Update update, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Ошибка обработки update:\n{exception}");
            var chatId = update.CallbackQuery?.Message?.Chat.Id
                ?? update.Message?.Chat.Id
                ?? update.CallbackQuery?.From.Id;
            if (chatId.HasValue)
                await BotMessenger.NotifyErrorAsync(botClient, chatId.Value, exception, ct);
        }

        private static string NormalizeCommand(string text)
        {
            var part = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            var atIndex = part.IndexOf('@');
            return atIndex > 0 ? part[..atIndex] : part;
        }
    }
}

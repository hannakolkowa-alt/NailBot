using Telegram.Bot;
using Telegram.Bot.Types;
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
                var isMasterAccount = RoleHelper.IsMasterAccount(userId);
                var actAsMaster = RoleHelper.ActAsMaster(chatId, userId);
                var normalized = MenuTexts.Normalize(text);

                Console.WriteLine($"Получено от {chatId}: {text} (роль: {RoleHelper.RoleLabel(chatId, userId)})");

                if (normalized.StartsWith('/'))
                {
                    SessionStore.Reset(chatId);
                    await CommandHandler.HandleAsync(
                        botClient, chatId, userId, NormalizeCommand(text),
                        message.From?.FirstName, message.From?.Username, ct);
                    return;
                }

                if (await RoleSwitchHandler.TryHandleAsync(botClient, chatId, userId, normalized, ct))
                    return;

                if (MenuTexts.IsMenuButton(normalized))
                {
                    var session = SessionStore.GetOrCreate(chatId);
                    var keepScheduleSession = actAsMaster
                        && (normalized is "расписание" or "график")
                        && (session.State is SessionState.Admin_Schedule_Date or SessionState.Admin_Schedule_Time
                            || session.Booking.WorkingDateId.HasValue);

                    if (!keepScheduleSession)
                        SessionStore.Reset(chatId);

                    if (actAsMaster && MenuTexts.IsClientOnlyButton(normalized))
                    {
                        await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
                        return;
                    }

                    if (actAsMaster)
                        await AdminHandler.HandleAsync(botClient, chatId, normalized, ct);
                    else
                        await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
                    return;
                }

                if (await SessionInputHandler.TryHandleAsync(botClient, chatId, userId, text, actAsMaster, ct))
                    return;

                if (actAsMaster)
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

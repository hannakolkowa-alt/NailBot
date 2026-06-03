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

                if (update.Message is not { } message)
                    return;

                long chatId = message.Chat.Id;
                long userId = message.From?.Id ?? chatId;

                if (message.Photo is { Length: > 0 }
                    && await GalleryAdminFlow.TryHandlePhotoAsync(botClient, message, ct))
                    return;

                if (message.Text is not { } text)
                    return;
                var isMasterAccount = RoleHelper.IsMasterAccount(userId);
                var actAsMaster = RoleHelper.ActAsMaster(chatId, userId);
                var normalized = MenuRouter.NormalizeForProfile(text, actAsMaster);

                Console.WriteLine($"Получено от {chatId}: {text} (роль: {RoleHelper.RoleLabel(chatId, userId)})");

                if (normalized.StartsWith('/'))
                {
                    SessionStore.Reset(chatId);
                    await CommandHandler.HandleAsync(
                        botClient, chatId, userId, NormalizeCommand(text),
                        message.From?.FirstName, message.From?.Username, ct);
                    return;
                }

                if (MenuTexts.IsMenuButton(normalized))
                {
                    var session = SessionStore.GetOrCreate(chatId);
                    var keepScheduleSession = actAsMaster
                        && (normalized is "расписание" or "график")
                        && session.State is SessionState.Admin_Schedule_CustomTime or SessionState.Admin_Schedule_EditTime;

                    var keepReviewSession = session.State is SessionState.Review_SelectStars or SessionState.Review_EnterText;
                    var keepGallerySession = actAsMaster
                        && normalized == "галерея"
                        && session.State == SessionState.Admin_Gallery_WaitPhoto;

                    if (!keepScheduleSession && !keepReviewSession && !keepGallerySession)
                        SessionStore.Reset(chatId);

                    if (MenuRouter.ShouldUseAdminHandler(normalized, actAsMaster, isMasterAccount))
                    {
                        await AdminHandler.HandleAsync(botClient, chatId, normalized, ct);
                        return;
                    }

                    if (MenuRouter.ShouldUseClientHandler(normalized, actAsMaster, isMasterAccount))
                    {
                        await ClientHandler.HandleAsync(botClient, chatId, userId, normalized, ct);
                        return;
                    }

                    await botClient.SendMessage(chatId,
                        actAsMaster
                            ? "Эта кнопка доступна в клиентском меню."
                            : "Эта кнопка доступна в меню мастера.",
                        replyMarkup: Keyboards.GetMenuForUser(chatId, userId),
                        cancellationToken: ct);
                    return;
                }

                if (await SessionInputHandler.TryHandleAsync(botClient, chatId, userId, text, actAsMaster, ct))
                    return;

                if (isMasterAccount && actAsMaster)
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

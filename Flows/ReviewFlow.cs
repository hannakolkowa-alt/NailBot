using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Flows
{
    public static class ReviewFlow
    {
        public static async Task BeginReviewAsync(ITelegramBotClient bot, long chatId, Guid appointmentId, CancellationToken ct)
        {
            if (await ReviewService.HasReviewForAppointmentAsync(appointmentId))
            {
                await bot.SendMessage(chatId,
                    "Вы уже оставили отзыв на этот визит.",
                    replyMarkup: Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(chatId)),
                    cancellationToken: ct);
                return;
            }

            var session = SessionStore.GetOrCreate(chatId);
            session.TargetAppointmentId = appointmentId;
            session.ReviewDraftRating = null;
            session.State = SessionState.Review_SelectStars;

            await bot.SendMessage(chatId,
                "Оцените визит от 1 до 5 звёзд:",
                replyMarkup: CreateStarsKeyboard(),
                cancellationToken: ct);
        }

        public static async Task OnStarSelectedAsync(ITelegramBotClient bot, long chatId, int stars, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.TargetAppointmentId.HasValue)
            {
                await bot.SendMessage(chatId, "Сессия устарела. Откройте «Мои записи».", cancellationToken: ct);
                return;
            }

            if (await ReviewService.HasReviewForAppointmentAsync(session.TargetAppointmentId.Value))
            {
                SessionStore.Reset(chatId);
                await bot.SendMessage(chatId, "Отзыв на этот визит уже есть.", cancellationToken: ct);
                return;
            }

            session.ReviewDraftRating = Math.Clamp(stars, 1, 5);
            session.State = SessionState.Review_EnterText;
            await bot.SendMessage(chatId,
                $"Выбрано: {ReviewService.FormatStars(stars)}\n\nНапишите комментарий к отзыву (или «-» без текста):",
                cancellationToken: ct);
        }

        public static async Task<bool> SaveReviewTextAsync(ITelegramBotClient bot, long chatId, long userId, string text, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.TargetAppointmentId.HasValue || !session.ReviewDraftRating.HasValue)
                return false;

            if (await ReviewService.HasReviewForAppointmentAsync(session.TargetAppointmentId.Value))
            {
                SessionStore.Reset(chatId);
                await bot.SendMessage(chatId, "Отзыв на этот визит уже был оставлен.", cancellationToken: ct);
                return true;
            }

            var comment = text.Trim();
            if (comment is "-" or "—" or "нет" or "пропустить")
                comment = "";

            var client = await ClientService.GetOrCreateAsync(userId, null, null);
            var ok = await ReviewService.AddAsync(
                client.ClientId,
                session.TargetAppointmentId,
                session.ReviewDraftRating.Value,
                comment);

            var starsLine = ReviewService.FormatStars(session.ReviewDraftRating);
            SessionStore.Reset(chatId);

            if (!ok)
            {
                await bot.SendMessage(chatId,
                    "Не удалось сохранить отзыв (возможно, он уже есть).",
                    replyMarkup: Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(userId)),
                    cancellationToken: ct);
                return true;
            }

            await bot.SendMessage(chatId,
                $"Спасибо за отзыв!\n{starsLine}",
                replyMarkup: Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(userId)),
                cancellationToken: ct);

            try
            {
                var body = string.IsNullOrEmpty(comment) ? "(без комментария)" : comment;
                await bot.SendMessage(BotConfig.PrimaryMasterTelegramId,
                    $"⭐ Новый отзыв {starsLine} от @{client.TelegramUsername}:\n{body}",
                    cancellationToken: ct);
            }
            catch { }

            return true;
        }

        public static async Task ShowPublicReviewsAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
        {
            var reviews = await ReviewService.GetAllAsync();
            var clients = await ClientService.GetAllClientsAsync();
            var kb = Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(userId));

            if (!reviews.Any())
            {
                await bot.SendMessage(chatId, "Пока нет отзывов. Будьте первым после визита!", replyMarkup: kb, cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId, $"⭐ Отзывы клиентов ({reviews.Count}):", cancellationToken: ct);
            foreach (var rev in reviews.Take(20))
            {
                var cl = clients.FirstOrDefault(c => c.ClientId == rev.ClientId);
                var block = ReviewService.FormatPublicReview(rev, cl);
                await bot.SendMessage(chatId, block, cancellationToken: ct);
            }

            if (reviews.Count > 20)
                await bot.SendMessage(chatId, $"… и ещё {reviews.Count - 20} отзывов.", replyMarkup: kb, cancellationToken: ct);
            else
                await bot.SendMessage(chatId, "Главное меню 👇", replyMarkup: kb, cancellationToken: ct);
        }

        public static InlineKeyboardMarkup CreateStarsKeyboard() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⭐ 1", "rev_star:1"),
                    InlineKeyboardButton.WithCallbackData("⭐ 2", "rev_star:2"),
                    InlineKeyboardButton.WithCallbackData("⭐ 3", "rev_star:3")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⭐ 4", "rev_star:4"),
                    InlineKeyboardButton.WithCallbackData("⭐ 5", "rev_star:5")
                },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "rev_cancel") }
            });
    }
}

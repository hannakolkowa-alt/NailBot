using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Constants;
using TelegramBot.Flows;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class ClientHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, long userId, string text, CancellationToken ct)
        {
            var kb = Keyboards.GetMenuForUser(chatId, userId);

            switch (text)
            {
                case "◀️ меню":
                    await ClientMenuFooter.SendAsync(botClient, chatId, userId, ct);
                    break;

                case "о мастере":
                    var profile = await MasterService.GetMasterProfileAsync();
                    if (profile == null)
                    {
                        await botClient.SendMessage(chatId, "Информация о мастере скоро появится.", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }
                    var nick = string.IsNullOrEmpty(profile.TelegramUsername) ? "" : $"@{profile.TelegramUsername.TrimStart('@')}";
                    var info = $"✨ О мастере\n\nИмя: {profile.Name}\nНик: {nick}\nОпыт: {profile.Experience}\n\n{profile.Description}";
                    await botClient.SendMessage(chatId, info, replyMarkup: kb, cancellationToken: ct);
                    break;

                case "портфолио мастера":
                case "партфолио мастера":
                    await GalleryAdminFlow.ShowPortfolioForClientAsync(botClient, chatId, userId, ct);
                    break;

                case "прайс":
                case "записаться":
                case "услуги":
                    await BookingFlow.StartAsync(botClient, chatId, ct);
                    break;

                case "график":
                case "расписание":
                    var now = DateTime.Now;
                    var schedule = await ScheduleService.FormatMonthScheduleAsync(now.Year, now.Month);
                    await botClient.SendMessage(chatId, schedule, replyMarkup: kb, cancellationToken: ct);
                    break;

                case "отзывы":
                    await ReviewFlow.ShowPublicReviewsAsync(botClient, chatId, userId, ct);
                    break;

                case "мои записи":
                    await ShowClientRecordsAsync(botClient, chatId, userId, ct);
                    break;

                default:
                    await botClient.SendMessage(chatId, "Выберите пункт меню 👇", replyMarkup: kb, cancellationToken: ct);
                    break;
            }
        }

        private static async Task ShowClientRecordsAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
        {
            var requests = await RequestService.GetByClientTelegramIdAsync(userId);
            var activeAppts = await AppointmentService.GetByClientTelegramIdAsync(userId);
            var completedAppts = await AppointmentService.GetCompletedByClientTelegramIdAsync(userId);
            var allRequests = await SupabaseConfig.GetClient().From<Models.Request>().Get();
            var session = State.SessionStore.GetOrCreate(chatId);
            session.CachedRequestIds = requests.Select(r => r.RequestId).ToList();

            var reviewable = new List<Models.Appointment>();
            foreach (var apt in completedAppts)
            {
                if (!await ReviewService.HasReviewForAppointmentAsync(apt.AppointmentId))
                    reviewable.Add(apt);
            }
            session.CachedReviewableAppointmentIds = reviewable.Select(a => a.AppointmentId).ToList();

            if (!requests.Any() && !activeAppts.Any() && !completedAppts.Any())
            {
                await bot.SendMessage(chatId, "У вас нет записей.", replyMarkup: Keyboards.GetMenuForUser(chatId, userId), cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId, "📋 Мои записи:", cancellationToken: ct);

            foreach (var (req, i) in requests.Select((r, idx) => (r, idx)))
            {
                var services = await CatalogService.GetRequestServicesAsync(req.RequestId);
                var svcNames = string.Join(", ", services.Select(s => s.Name));
                var msg = $"📋 Запись #{i + 1}\nСтатус: {RequestStatus.ToDisplayRussian(req.Status)}\nУслуги: {svcNames}\nДата: {req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}\n{req.Comment}";

                var rows = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Отмена", $"rec_can:{i}"),
                        InlineKeyboardButton.WithCallbackData("Изменить", $"rec_chg:{i}")
                    }
                };
                await bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }

            foreach (var (apt, ri) in completedAppts.Select((a, idx) => (a, idx)))
            {
                var req = allRequests.Models?.FirstOrDefault(r => r.RequestId == apt.RequestId);
                var services = req != null
                    ? await CatalogService.GetRequestServicesAsync(req.RequestId)
                    : new List<Models.Service>();
                var svcNames = services.Any() ? string.Join(", ", services.Select(s => s.Name)) : "—";
                var dateStr = req?.DesiredDate != null
                    ? $"{req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}"
                    : "дата уточняется";

                var hasReview = await ReviewService.HasReviewForAppointmentAsync(apt.AppointmentId);
                var statusLine = hasReview ? "✅ Выполнено · отзыв оставлен" : "✅ Выполнено";

                await bot.SendMessage(chatId,
                    $"📋 Визит\n{statusLine}\nУслуги: {svcNames}\nДата: {dateStr}",
                    cancellationToken: ct);

                if (!hasReview)
                {
                    var revIdx = session.CachedReviewableAppointmentIds.IndexOf(apt.AppointmentId);
                    if (revIdx >= 0)
                    {
                        await bot.SendMessage(chatId, "Оцените визит:",
                            replyMarkup: new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("⭐ Оставить отзыв", $"rev_start:{revIdx}") }
                            }),
                            cancellationToken: ct);
                    }
                }
            }

            foreach (var apt in activeAppts.Where(a => !completedAppts.Any(c => c.AppointmentId == a.AppointmentId)))
            {
                var req = allRequests.Models?.FirstOrDefault(r => r.RequestId == apt.RequestId);
                var services = req != null
                    ? await CatalogService.GetRequestServicesAsync(req.RequestId)
                    : new List<Models.Service>();
                await bot.SendMessage(chatId,
                    $"📋 Подтверждено\nУслуги: {string.Join(", ", services.Select(s => s.Name))}\nДата: {req?.DesiredDate:dd.MM.yyyy} {req?.DesiredTime:HH:mm}",
                    cancellationToken: ct);
            }

            await ClientMenuFooter.SendAsync(bot, chatId, userId, ct);
        }
    }
}

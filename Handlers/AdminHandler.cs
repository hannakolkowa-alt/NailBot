using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class AdminHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, string text, CancellationToken ct)
        {
            var kb = Keyboards.CreateAdminMenuKeyboard();
            var session = SessionStore.GetOrCreate(chatId);

            switch (text)
            {
                case "◀️ меню":
                    SessionStore.Reset(chatId);
                    await botClient.SendMessage(chatId, "Меню мастера 👇", replyMarkup: kb, cancellationToken: ct);
                    break;

                case "клиентская база":
                    var clients = await ClientService.GetAllClientsAsync();
                    session.CachedClientIds = clients.Select(c => c.ClientId).ToList();

                    if (!clients.Any())
                    {
                        await botClient.SendMessage(chatId, "Клиентская база пуста.", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }

                    var lines = clients.Select((c, i) => $"{i + 1}. @{c.TelegramUsername} ({c.FirstName})");
                    await botClient.SendMessage(chatId, "👥 Клиенты:\n" + string.Join("\n", lines), cancellationToken: ct);

                    var delRows = clients.Select((c, i) =>
                        new[] { InlineKeyboardButton.WithCallbackData($"❌ Удалить @{c.TelegramUsername}", $"cli_del:{i}") }).ToList();
                    delRows.Add(new[] { InlineKeyboardButton.WithCallbackData("🗑 Очистить всё", "cli_clear") });
                    await botClient.SendMessage(chatId, "Управление:", replyMarkup: new InlineKeyboardMarkup(delRows), cancellationToken: ct);
                    break;

                case "записи":
                    await ShowAppointmentsAsync(botClient, chatId, ct);
                    await SendMasterMenuFooterAsync(botClient, chatId, ct);
                    break;

                case "заявки":
                    await ShowPendingRequestsAsync(botClient, chatId, ct);
                    await SendMasterMenuFooterAsync(botClient, chatId, ct);
                    break;

                case "мой профиль":
                    var profile = await MasterService.GetMasterProfileAsync();
                    if (profile == null)
                    {
                        session.State = SessionState.Admin_Profile_Name;
                        await botClient.SendMessage(chatId, "Профиля нет. Введите имя мастера:", cancellationToken: ct);
                    }
                    else
                    {
                        var nick = string.IsNullOrEmpty(profile.TelegramUsername) ? "" : $"@{profile.TelegramUsername.TrimStart('@')}";
                        await botClient.SendMessage(chatId,
                            $"👤 Профиль:\nИмя: {profile.Name}\nНик: {nick}\nОпыт: {profile.Experience}\n\n{profile.Description}",
                            replyMarkup: kb, cancellationToken: ct);
                    }
                    break;

                case "изменить профиль":
                    var p = await MasterService.GetMasterProfileAsync();
                    if (p == null)
                    {
                        await botClient.SendMessage(chatId, "Сначала создайте профиль («Мой профиль»).", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }
                    var editKb = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Имя", "prof:name") },
                        new[] { InlineKeyboardButton.WithCallbackData("Ник", "prof:user") },
                        new[] { InlineKeyboardButton.WithCallbackData("Опыт", "prof:exp") },
                        new[] { InlineKeyboardButton.WithCallbackData("Описание", "prof:desc") }
                    });
                    await botClient.SendMessage(chatId, "Что изменить?", replyMarkup: editKb, cancellationToken: ct);
                    break;

                case "услуги":
                    await ServicesAdminFlow.ShowMenuAsync(botClient, chatId, ct);
                    await SendMasterMenuFooterAsync(botClient, chatId, ct);
                    break;

                case "расписание":
                case "график":
                    await ScheduleAdminFlow.ShowCalendarAsync(botClient, chatId, ct: ct);
                    await SendMasterMenuFooterAsync(botClient, chatId, ct);
                    break;

                case "все отзывы":
                case "отзывы":
                    var reviews = await ReviewService.GetAllAsync();
                    var clientsAll = await ClientService.GetAllClientsAsync();
                    if (!reviews.Any())
                    {
                        await botClient.SendMessage(chatId, "Отзывов пока нет.", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }
                    await botClient.SendMessage(chatId, $"⭐ Отзывы клиентов ({reviews.Count}):", cancellationToken: ct);
                    foreach (var rev in reviews)
                    {
                        var cl = clientsAll.FirstOrDefault(c => c.ClientId == rev.ClientId);
                        var un = cl != null ? $"@{cl.TelegramUsername}" : "клиент";
                        var stars = ReviewService.FormatStars(rev.Rating);
                        var body = string.IsNullOrWhiteSpace(rev.Text) ? "(без комментария)" : rev.Text;
                        await botClient.SendMessage(chatId, $"{stars} {un}:\n{body}", cancellationToken: ct);
                    }
                    await SendMasterMenuFooterAsync(botClient, chatId, ct);
                    break;

                default:
                    await botClient.SendMessage(chatId, "Выберите пункт меню.", replyMarkup: kb, cancellationToken: ct);
                    break;
            }
        }

        private static async Task ShowPendingRequestsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var requests = await RequestService.GetPendingRequestsAsync();
            var session = SessionStore.GetOrCreate(chatId);
            session.CachedRequestIds = requests.Select(r => r.RequestId).ToList();

            if (!requests.Any())
            {
                await bot.SendMessage(chatId, "Новых заявок нет.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            var clients = await ClientService.GetAllClientsAsync();
            foreach (var (req, i) in requests.Select((r, idx) => (r, idx)))
            {
                var services = await CatalogService.GetRequestServicesAsync(req.RequestId);
                var cl = clients.FirstOrDefault(c => c.ClientId == req.ClientId);
                var msg = $"📩 Заявка\nУслуги: {string.Join(", ", services.Select(s => s.Name))}\nДата: {req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}\n{req.Comment}";

                var rows = new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"req_ok:{req.RequestId}"),
                        InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"req_no:{req.RequestId}")
                    }
                };
                await bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }
        }

        private static async Task ShowAppointmentsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var appts = await AppointmentService.GetActiveAppointmentsAsync();
            var session = SessionStore.GetOrCreate(chatId);
            session.CachedAppointmentIds = appts.Select(a => a.AppointmentId).ToList();
            var clients = await ClientService.GetAllClientsAsync();
            var allRequests = await SupabaseConfig.GetClient().From<Models.Request>().Get();
            var approvedRequests = await RequestService.GetApprovedRequestsAsync();

            if (appts.Any())
            {
                foreach (var (apt, i) in appts.Select((a, idx) => (a, idx)))
                {
                    var req = allRequests.Models?.FirstOrDefault(r => r.RequestId == apt.RequestId);
                    var cl = clients.FirstOrDefault(c => c.ClientId == apt.ClientId);
                    var svc = req != null ? await CatalogService.GetRequestServicesAsync(req.RequestId) : new List<Models.Service>();
                    var msg = $"📋 Запись\nКлиент: {cl?.FirstName} @{cl?.TelegramUsername}\nУслуги: {string.Join(", ", svc.Select(s => s.Name))}\nДата: {req?.DesiredDate:dd.MM.yyyy} {req?.DesiredTime:HH:mm}";

                    await bot.SendMessage(chatId, msg,
                        replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("✅ Выполнено", $"apt_done:{i}") }
                        }),
                        cancellationToken: ct);
                }
                return;
            }

            if (approvedRequests.Any())
            {
                await bot.SendMessage(chatId, "📋 Одобренные заявки (календарь):", cancellationToken: ct);
                foreach (var req in approvedRequests)
                {
                    var cl = clients.FirstOrDefault(c => c.ClientId == req.ClientId);
                    var svc = await CatalogService.GetRequestServicesAsync(req.RequestId);
                    await bot.SendMessage(chatId,
                        $"• {cl?.FirstName} @{cl?.TelegramUsername}\n{string.Join(", ", svc.Select(s => s.Name))}\n{req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}",
                        cancellationToken: ct);
                }
                return;
            }

            await bot.SendMessage(chatId, "Активных записей нет.", cancellationToken: ct);
        }

        private static async Task SendMasterMenuFooterAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await bot.SendMessage(chatId, "Меню мастера 👇",
                replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                cancellationToken: ct);
        }
    }
}

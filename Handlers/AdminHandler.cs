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
                    await botClient.SendMessage(chatId,
                        "Меню мастера 👇\nКлиентское меню — /client",
                        replyMarkup: kb,
                        cancellationToken: ct);
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
                    break;

                case "заявки":
                    await ShowPendingRequestsAsync(botClient, chatId, ct);
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
                    var all = await CatalogService.GetAllServicesAsync();
                    var cats = await CatalogService.GetCategoriesAsync();
                    var priceText = string.Join("\n", all.Select(s =>
                    {
                        var cat = cats.FirstOrDefault(c => c.CategoryId == s.CategoryId)?.Name ?? "";
                        return $"• [{cat}] {s.Name} — {s.Price}₽ ({s.DurationMinutes} мин)\n  {s.Description}";
                    }));
                    await botClient.SendMessage(chatId, "💰 Услуги:\n" + (priceText.Length > 0 ? priceText : "(пусто)"), cancellationToken: ct);

                    session.AdminCategoryId = cats.FirstOrDefault()?.CategoryId;
                    session.State = SessionState.Admin_Service_Name;
                    await botClient.SendMessage(chatId, "➕ Добавить услугу — введите название:", cancellationToken: ct);
                    break;

                case "расписание":
                case "график":
                    session.State = SessionState.Admin_Schedule_Date;
                    await botClient.SendMessage(chatId,
                        "📅 Введите дату рабочего дня:\n" +
                        "• ГГГГ-ММ-ДД (2026-06-01)\n" +
                        "• или ДД.ММ.ГГГГ (01.06.2026)\n\n" +
                        "Затем введите время (10:00, 14:30 …).\n" +
                        "Когда все слоты добавлены — «готово».",
                        cancellationToken: ct);
                    break;

                case "отзывы":
                    var reviews = await ReviewService.GetAllAsync();
                    var clientsAll = await ClientService.GetAllClientsAsync();
                    if (!reviews.Any())
                    {
                        await botClient.SendMessage(chatId, "Отзывов пока нет.", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }
                    foreach (var rev in reviews)
                    {
                        var cl = clientsAll.FirstOrDefault(c => c.ClientId == rev.ClientId);
                        var un = cl != null ? $"@{cl.TelegramUsername}" : "клиент";
                        await botClient.SendMessage(chatId, $"⭐ {un}:\n{rev.Text}", cancellationToken: ct);
                    }
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
                        InlineKeyboardButton.WithCallbackData("✅ Одобрить", $"req_ok:{i}"),
                        InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"req_no:{i}")
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
            var requests = await SupabaseConfig.GetClient().From<Models.Request>().Get();

            if (!appts.Any())
            {
                await bot.SendMessage(chatId, "Активных записей нет.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            foreach (var (apt, i) in appts.Select((a, idx) => (a, idx)))
            {
                var req = requests.Models?.FirstOrDefault(r => r.RequestId == apt.RequestId);
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
        }
    }
}

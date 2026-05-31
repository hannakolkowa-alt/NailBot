using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Flows
{
    public static class BookingFlow
    {
        public static async Task StartAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            SessionStore.Reset(chatId);
            session.State = SessionState.Idle;

            var cats = await CatalogService.GetCategoriesAsync();
            session.CachedCategoryIds = cats
                .Where(c => c.Name != CatalogService.AdditionalCategoryName)
                .Select(c => c.CategoryId)
                .ToList();

            if (!session.CachedCategoryIds.Any())
            {
                await bot.SendMessage(chatId, "Услуги пока не добавлены мастером.", cancellationToken: ct);
                return;
            }

            var rows = session.CachedCategoryIds.Select((id, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(
                    cats.First(c => c.CategoryId == id).Name, $"cat:{i}") }).ToArray();

            rows = rows.Append(new[] { InlineKeyboardButton.WithCallbackData("Дополнительно", "add:menu") }).ToArray();

            await bot.SendMessage(chatId,
                "💅 Выберите категорию услуг:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowCategoryServicesAsync(ITelegramBotClient bot, long chatId, int catIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (catIndex < 0 || catIndex >= session.CachedCategoryIds.Count) return;

            session.CurrentCategoryIndex = catIndex;
            var catId = session.CachedCategoryIds[catIndex];
            var services = await CatalogService.GetByCategoryAsync(catId);
            session.CachedServiceIds = services.Select(s => s.ServiceId).ToList();

            var rows = new List<InlineKeyboardButton[]>();
            for (int i = 0; i < services.Count; i++)
            {
                var s = services[i];
                var mark = session.Booking.SelectedServiceIds.Contains(s.ServiceId) ? "✅ " : "";
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData(
                    $"{mark}{s.Name} — {s.Price}₽",
                    $"svc:{i}") });
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Готово → дата", "svc:done") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Категории", "book:restart") });

            await bot.SendMessage(chatId,
                "Выберите услугу (можно несколько). Нажмите снова, чтобы снять выбор:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowAdditionalAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var services = await CatalogService.GetAdditionalServicesAsync();
            session.CachedServiceIds = services.Select(s => s.ServiceId).ToList();

            var rows = new List<InlineKeyboardButton[]>();
            for (int i = 0; i < services.Count; i++)
            {
                var s = services[i];
                var mark = session.Booking.SelectedServiceIds.Contains(s.ServiceId) ? "✅ " : "";
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"{mark}{s.Name}", $"add:{i}") });
            }
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Готово → дата", "add:done") });

            await bot.SendMessage(chatId, "➕ Дополнительные услуги:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }

        public static async Task ShowDatesAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.Booking.SelectedServiceIds.Any())
            {
                await bot.SendMessage(chatId, "Сначала выберите хотя бы одну услугу.", cancellationToken: ct);
                return;
            }

            var from = DateOnly.FromDateTime(DateTime.Today);
            var to = from.AddMonths(1);
            var dates = await ScheduleService.GetWorkingDatesAsync(from, to);
            session.CachedDateIds = dates.Select(d => d.DateId).ToList();

            if (!dates.Any())
            {
                await bot.SendMessage(chatId, "Нет свободных дат. Мастер ещё не создал расписание.", cancellationToken: ct);
                return;
            }

            var rows = dates.Select((d, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(d.Date.ToString("dd.MM.yyyy"), $"date:{i}") }).ToArray();

            await bot.SendMessage(chatId, "📅 Выберите дату:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }

        public static async Task ShowTimesAsync(ITelegramBotClient bot, long chatId, int dateIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (dateIndex < 0 || dateIndex >= session.CachedDateIds.Count) return;

            var dateId = session.CachedDateIds[dateIndex];
            var wdList = await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
            var wd = wdList.FirstOrDefault(w => w.DateId == dateId);
            if (wd == null) return;

            session.Booking.WorkingDateId = dateId;
            session.Booking.Date = wd.Date;

            var slots = await ScheduleService.GetFreeSlotsAsync(dateId);
            session.CachedSlotIds = slots.Select(s => s.TimeSlotId).ToList();

            if (!slots.Any())
            {
                await bot.SendMessage(chatId, "На эту дату нет свободных слотов.", cancellationToken: ct);
                return;
            }

            var rows = slots.Select((s, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(s.Time.ToString("HH:mm"), $"slot:{i}") }).ToArray();

            await bot.SendMessage(chatId, "🕐 Выберите время:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }

        public static async Task AskNameAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            session.State = SessionState.Booking_EnterName;
            await bot.SendMessage(chatId, "✏️ Введите ваше имя:", cancellationToken: ct);
        }

        public static async Task AskUsernameAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            session.State = SessionState.Booking_EnterUsername;
            await bot.SendMessage(chatId, "✏️ Введите ваш ник в Telegram (например: @wannxxl):", cancellationToken: ct);
        }

        public static async Task ShowSummaryAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var b = session.Booking;
            var services = await CatalogService.FormatServiceListAsync(b.SelectedServiceIds);
            var date = b.Date?.ToString("dd.MM.yyyy") ?? "—";
            var time = b.Time?.ToString("HH:mm") ?? "—";
            var nick = b.TelegramNick?.StartsWith('@') == true ? b.TelegramNick : $"@{b.TelegramNick}";

            var text = $"Услуги: {services}.\nЗапись на дату {date} время {time}.\nДанные для связи: {b.ClientName} {nick}.\n\nЕсли всё верно, подтвердите заявку.";

            await bot.SendMessage(chatId, text,
                replyMarkup: Keyboards.ConfirmCancel("book:ok", "book:restart"),
                cancellationToken: ct);
        }

        public static async Task SubmitAsync(ITelegramBotClient bot, long chatId, long userId, string? tgUsername, string? firstName, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var b = session.Booking;

            var contact = $"{b.ClientName} {b.TelegramNick}";
            var ids = b.SelectedServiceIds.ToList();

            if (b.EditingRequestId.HasValue)
            {
                await RequestService.DeleteRequestAsync(b.EditingRequestId.Value);
            }

            var requestId = await RequestService.CreateRequestAsync(
                userId, tgUsername ?? "", firstName ?? b.ClientName ?? "Клиент",
                b.Date, b.Time, ids, contact, b.WorkingDateId, b.TimeSlotId);

            SessionStore.Reset(chatId);

            if (requestId == null)
            {
                await bot.SendMessage(chatId, "Ошибка при создании заявки. Попробуйте позже.", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId,
                "✅ Заявка отправлена мастеру на подтверждение!",
                replyMarkup: Keyboards.CreateMainMenuKeyboard(),
                cancellationToken: ct);

            try
            {
                await bot.SendMessage(BotConfig.PrimaryMasterTelegramId,
                    "📩 Новая заявка на запись! Откройте раздел «Заявки».",
                    cancellationToken: ct);
            }
            catch { /* admin may not have started bot */ }
        }
    }
}

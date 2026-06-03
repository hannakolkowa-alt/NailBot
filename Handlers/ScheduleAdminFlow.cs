using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class ScheduleAdminFlow
    {
        public static async Task ShowCalendarAsync(ITelegramBotClient bot, long chatId, int? year = null, int? month = null, CancellationToken ct = default)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var now = DateTime.Now;
            var y = year ?? (session.ScheduleCalendarYear > 0 ? session.ScheduleCalendarYear : now.Year);
            var m = month ?? (session.ScheduleCalendarMonth > 0 ? session.ScheduleCalendarMonth : now.Month);

            session.ScheduleCalendarYear = y;
            session.ScheduleCalendarMonth = m;
            session.State = SessionState.Idle;

            var scheduled = await ScheduleService.GetScheduledDatesInMonthAsync(y, m);
            var summary = await ScheduleService.FormatMonthScheduleAsync(y, m);
            var today = DateOnly.FromDateTime(now);

            await bot.SendMessage(chatId,
                summary + "\n\n📅 Выберите день в календаре:\n• — сегодня, ✓ — есть слоты",
                replyMarkup: ScheduleCalendarKeyboard.Build(y, m, scheduled, today),
                cancellationToken: ct);
        }

        public static async Task ShowDayAsync(ITelegramBotClient bot, long chatId, DateOnly date, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var master = await MasterService.EnsureMasterExistsAsync("Мастер", null);

            var wd = await ScheduleService.GetWorkingDateByDateAsync(master.MasterId, date)
                ?? await ScheduleService.AddWorkingDateAsync(master.MasterId, date);

            if (wd == null)
            {
                await bot.SendMessage(chatId, "Не удалось открыть дату.", cancellationToken: ct);
                return;
            }

            session.Booking.WorkingDateId = wd.DateId;
            session.ScheduleCalendarYear = date.Year;
            session.ScheduleCalendarMonth = date.Month;

            var slots = await ScheduleService.GetSlotsForWorkingDateAsync(wd.DateId);
            session.CachedSlotIds = slots.Select(s => s.TimeSlotId).ToList();

            var bookedIds = await ScheduleService.GetBookedSlotIdsAsync(wd.DateId);
            var lines = new List<string> { $"📅 {date:dd.MM.yyyy}" };

            if (!slots.Any())
                lines.Add("\nСлотов пока нет. Нажмите «➕ Добавить время».");
            else
            {
                lines.Add("\nВремя:");
                foreach (var (s, i) in slots.Select((s, idx) => (s, idx)))
                {
                    var busy = bookedIds.Contains(s.TimeSlotId) || s.IsBooked;
                    lines.Add(busy ? $"  🔒 {s.Time:HH:mm} — занято" : $"  • {s.Time:HH:mm}");
                }
            }

            var rows = new List<InlineKeyboardButton[]>
            {
                new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить время", "sch_add") },
            };

            foreach (var (s, i) in slots.Select((s, idx) => (s, idx)))
            {
                var busy = bookedIds.Contains(s.TimeSlotId) || s.IsBooked;
                if (busy) continue;

                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"✏️ {s.Time:HH:mm}", $"sch_es:{i}"),
                    InlineKeyboardButton.WithCallbackData($"🗑 {s.Time:HH:mm}", $"sch_ds:{i}")
                });
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить весь день", "sch_dd") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ К календарю", $"sch_m:{date.Year}:{date.Month}") });

            await bot.SendMessage(chatId, string.Join("\n", lines),
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowTimePickerAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.Booking.WorkingDateId.HasValue)
            {
                await ShowCalendarAsync(bot, chatId, ct: ct);
                return;
            }

            var existing = await ScheduleService.GetSlotsForWorkingDateAsync(session.Booking.WorkingDateId.Value);
            var existingTimes = existing.Select(s => s.Time).ToHashSet();
            var suggested = await ScheduleService.GetSuggestedTimesAsync();
            var available = suggested.Where(t => !existingTimes.Contains(t)).ToList();

            var rows = new List<InlineKeyboardButton[]>();
            for (var i = 0; i < available.Count; i += 4)
            {
                var chunk = available.Skip(i).Take(4).Select(t =>
                    InlineKeyboardButton.WithCallbackData(t.ToString("HH:mm"), $"sch_t:{ScheduleTimeParser.ToCallbackKey(t)}"));
                rows.Add(chunk.ToArray());
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✏️ Своё время (ввод)", "sch_tc") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад к дню", "sch_back_day") });

            await bot.SendMessage(chatId,
                "🕐 Выберите время из списка (из базы расписания) или введите своё:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task AddTimeAsync(ITelegramBotClient bot, long chatId, TimeOnly time, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.Booking.WorkingDateId.HasValue)
            {
                await bot.SendMessage(chatId, "Сначала выберите дату в календаре.", cancellationToken: ct);
                return;
            }

            try
            {
                var slot = await ScheduleService.AddTimeSlotAsync(session.Booking.WorkingDateId.Value, time);
                if (slot == null)
                {
                    var err = ScheduleService.LastSlotError ?? "неизвестная ошибка";
                    await bot.SendMessage(chatId, $"Не удалось добавить слот.\n{err}", cancellationToken: ct);
                    return;
                }

                await bot.SendMessage(chatId, $"✅ Время {time:HH:mm} добавлено.", cancellationToken: ct);

                var wdList = await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
                var wd = wdList.FirstOrDefault(d => d.DateId == session.Booking.WorkingDateId);
                if (wd != null)
                    await ShowDayAsync(bot, chatId, wd.Date, ct);
            }
            catch (Exception ex)
            {
                await bot.SendMessage(chatId, $"⚠️ Ошибка: {ex.Message}", cancellationToken: ct);
            }
        }

        public static async Task BeginCustomTimeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            session.State = SessionState.Admin_Schedule_CustomTime;
            await bot.SendMessage(chatId, "Введите время (ЧЧ:ММ), например 10:30 или 14:00:", cancellationToken: ct);
        }

        public static async Task BeginEditSlotAsync(ITelegramBotClient bot, long chatId, int slotIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (slotIndex < 0 || slotIndex >= session.CachedSlotIds.Count)
            {
                await bot.SendMessage(chatId, "Слот не найден. Откройте день снова.", cancellationToken: ct);
                return;
            }

            session.TargetSlotId = session.CachedSlotIds[slotIndex];
            session.State = SessionState.Admin_Schedule_EditTime;
            await bot.SendMessage(chatId, "Введите новое время (ЧЧ:ММ):", cancellationToken: ct);
        }

        public static async Task DeleteSlotAsync(ITelegramBotClient bot, long chatId, int slotIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (slotIndex < 0 || slotIndex >= session.CachedSlotIds.Count)
            {
                await bot.SendMessage(chatId, "Слот не найден.", cancellationToken: ct);
                return;
            }

            var slotId = session.CachedSlotIds[slotIndex];
            var ok = await ScheduleService.DeleteTimeSlotAsync(slotId);
            if (!ok)
            {
                await bot.SendMessage(chatId, "Нельзя удалить: слот занят записью клиента.", cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId, "✅ Слот удалён.", cancellationToken: ct);
            await RefreshCurrentDayAsync(bot, chatId, ct);
        }

        public static async Task ConfirmDeleteDayAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Да, удалить день", "sch_dd_yes"),
                    InlineKeyboardButton.WithCallbackData("Отмена", "sch_back_day")
                }
            });
            await bot.SendMessage(chatId, "Удалить весь день и все свободные слоты?", replyMarkup: kb, cancellationToken: ct);
        }

        public static async Task DeleteDayAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.Booking.WorkingDateId.HasValue)
            {
                await ShowCalendarAsync(bot, chatId, ct: ct);
                return;
            }

            var (ok, err) = await ScheduleService.DeleteWorkingDateAsync(session.Booking.WorkingDateId.Value);
            session.Booking.WorkingDateId = null;

            if (!ok)
            {
                await bot.SendMessage(chatId, err ?? "Не удалось удалить день.", cancellationToken: ct);
                await RefreshCurrentDayAsync(bot, chatId, ct);
                return;
            }

            await bot.SendMessage(chatId, "✅ День удалён из расписания.", cancellationToken: ct);
            await ShowCalendarAsync(bot, chatId, session.ScheduleCalendarYear, session.ScheduleCalendarMonth, ct);
        }

        public static async Task RefreshCurrentDayAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.Booking.WorkingDateId.HasValue)
            {
                await ShowCalendarAsync(bot, chatId, ct: ct);
                return;
            }

            var wdList = await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
            var wd = wdList.FirstOrDefault(d => d.DateId == session.Booking.WorkingDateId);
            if (wd != null)
                await ShowDayAsync(bot, chatId, wd.Date, ct);
            else
                await ShowCalendarAsync(bot, chatId, session.ScheduleCalendarYear, session.ScheduleCalendarMonth, ct);
        }

        public static bool TryParseDayCallback(string data, out DateOnly date)
        {
            date = default;
            if (!data.StartsWith("sch_d:"))
                return false;
            return DateOnly.TryParse(data[6..], out date);
        }

        public static bool TryParseMonthCallback(string data, out int year, out int month)
        {
            year = 0;
            month = 0;
            string prefix;
            if (data.StartsWith("sch_m:")) prefix = "sch_m:";
            else if (data.StartsWith("sch_mp:")) prefix = "sch_mp:";
            else if (data.StartsWith("sch_mn:")) prefix = "sch_mn:";
            else return false;

            var parts = data[prefix.Length..].Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out year) || !int.TryParse(parts[1], out month))
                return false;
            return month is >= 1 and <= 12;
        }
    }
}

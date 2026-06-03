using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Flows
{
    public static class AppointmentRescheduleFlow
    {
        public static async Task BeginAsync(ITelegramBotClient bot, long chatId, Guid appointmentId, CancellationToken ct)
        {
            var apt = await AppointmentService.GetByIdAsync(appointmentId);
            if (apt == null)
            {
                await bot.SendMessage(chatId, "Запись не найдена.", cancellationToken: ct);
                return;
            }

            var req = apt.RequestId.HasValue
                ? await RequestService.GetByIdAsync(apt.RequestId.Value)
                : null;

            var session = SessionStore.GetOrCreate(chatId);
            session.TargetAppointmentId = appointmentId;
            session.TargetSlotId = apt.TimeSlotId;
            session.Booking.WorkingDateId = null;
            session.Booking.TimeSlotId = null;
            session.Booking.Date = null;
            session.Booking.Time = null;
            session.TempText = req != null
                ? $"{req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}"
                : "—";
            session.State = SessionState.Admin_Reschedule_PickDate;

            await ShowDatesAsync(bot, chatId, ct);
        }

        public static async Task ShowDatesAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.TargetAppointmentId.HasValue)
            {
                await CancelAsync(bot, chatId, ct);
                return;
            }

            var from = DateOnly.FromDateTime(DateTime.Today);
            var to = from.AddMonths(2);
            var dates = await ScheduleService.GetWorkingDatesAsync(from, to);
            var aptId = session.TargetAppointmentId.Value;
            var apt = await AppointmentService.GetByIdAsync(aptId);

            var available = new List<Models.WorkingDate>();
            foreach (var d in dates)
            {
                var includeSlot = apt != null && apt.WorkingDateId == d.DateId ? apt.TimeSlotId : (Guid?)null;
                var slots = await ScheduleService.GetSlotsForRescheduleAsync(d.DateId, aptId, includeSlot);
                if (slots.Any())
                    available.Add(d);
            }

            if (!available.Any())
            {
                session.State = SessionState.Idle;
                await bot.SendMessage(chatId,
                    "Нет дат со свободными слотами. Добавьте расписание в «Расписание».",
                    replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }

            session.CachedDateIds = available.Select(d => d.DateId).ToList();
            var rows = available.Select((d, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(d.Date.ToString("dd.MM.yyyy"), $"rsch_date:{i}") }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "rsch_cancel") });

            await bot.SendMessage(chatId,
                $"📅 Перенос записи\nБыло: {session.TempText}\n\nВыберите новую дату:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowTimesAsync(ITelegramBotClient bot, long chatId, int dateIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.TargetAppointmentId.HasValue
                || dateIndex < 0
                || dateIndex >= session.CachedDateIds.Count)
            {
                await StaleAsync(bot, chatId, ct);
                return;
            }

            var aptId = session.TargetAppointmentId.Value;
            var apt = await AppointmentService.GetByIdAsync(aptId);
            var dateId = session.CachedDateIds[dateIndex];
            var wdList = await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
            var wd = wdList.FirstOrDefault(w => w.DateId == dateId);
            if (wd == null)
            {
                await StaleAsync(bot, chatId, ct);
                return;
            }

            session.Booking.WorkingDateId = dateId;
            session.Booking.Date = wd.Date;

            var includeSlot = apt != null && apt.WorkingDateId == dateId ? apt.TimeSlotId : (Guid?)null;
            var slots = await ScheduleService.GetSlotsForRescheduleAsync(dateId, aptId, includeSlot);
            session.CachedSlotIds = slots.Select(s => s.TimeSlotId).ToList();

            if (!slots.Any())
            {
                await bot.SendMessage(chatId, "На эту дату нет свободных слотов. Выберите другую дату:",
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("◀️ К датам", "rsch_back_dates") }
                    }),
                    cancellationToken: ct);
                return;
            }

            session.State = SessionState.Admin_Reschedule_PickTime;
            var rows = slots.Select((s, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(s.Time.ToString("HH:mm"), $"rsch_time:{i}") }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ К датам", "rsch_back_dates") });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "rsch_cancel") });

            await bot.SendMessage(chatId,
                $"🕐 Дата: {wd.Date:dd.MM.yyyy}\nВыберите новое время:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowConfirmAsync(ITelegramBotClient bot, long chatId, int slotIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (slotIndex < 0 || slotIndex >= session.CachedSlotIds.Count || !session.Booking.WorkingDateId.HasValue)
            {
                await StaleAsync(bot, chatId, ct);
                return;
            }

            var slotId = session.CachedSlotIds[slotIndex];
            var slots = await ScheduleService.GetSlotsForWorkingDateAsync(session.Booking.WorkingDateId.Value);
            var slot = slots.FirstOrDefault(s => s.TimeSlotId == slotId);
            if (slot == null)
            {
                await StaleAsync(bot, chatId, ct);
                return;
            }

            session.Booking.TimeSlotId = slotId;
            session.Booking.Time = slot.Time;
            session.State = SessionState.Admin_Reschedule_Confirm;

            var newWhen = $"{session.Booking.Date:dd.MM.yyyy} {session.Booking.Time:HH:mm}";
            await bot.SendMessage(chatId,
                $"📋 Подтвердите перенос\n\nБыло: {session.TempText}\nСтало: {newWhen}",
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("✅ Подтвердить", "rsch_ok"),
                        InlineKeyboardButton.WithCallbackData("❌ Отмена", "rsch_cancel")
                    }
                }),
                cancellationToken: ct);
        }

        public static async Task ConfirmAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (!session.TargetAppointmentId.HasValue
                || !session.Booking.WorkingDateId.HasValue
                || !session.Booking.TimeSlotId.HasValue
                || !session.Booking.Date.HasValue
                || !session.Booking.Time.HasValue)
            {
                await StaleAsync(bot, chatId, ct);
                return;
            }

            var aptId = session.TargetAppointmentId.Value;
            var oldWhen = session.TempText ?? "—";
            var newDate = session.Booking.Date.Value;
            var newTime = session.Booking.Time.Value;

            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                await bot.SendMessage(chatId, "Профиль мастера не найден.", cancellationToken: ct);
                return;
            }

            var newSlotId = session.Booking.TimeSlotId.Value;
            var wdId = session.Booking.WorkingDateId.Value;

            var existingSlots = await ScheduleService.GetSlotsForWorkingDateAsync(wdId);
            if (!existingSlots.Any(s => s.TimeSlotId == newSlotId))
            {
                var created = await ScheduleService.AddTimeSlotAsync(wdId, newTime);
                if (created == null)
                {
                    await bot.SendMessage(chatId, "Не удалось создать слот на выбранное время.", cancellationToken: ct);
                    return;
                }
                newSlotId = created.TimeSlotId;
            }

            var (ok, err) = await AppointmentService.RescheduleAsync(
                aptId, wdId, newSlotId, newDate, newTime);

            SessionStore.Reset(chatId);

            if (!ok)
            {
                await bot.SendMessage(chatId,
                    $"Не удалось перенести запись. {err ?? ""}",
                    replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }

            var apt = await AppointmentService.GetByIdAsync(aptId);
            if (apt != null)
            {
                var client = (await ClientService.GetAllClientsAsync())
                    .FirstOrDefault(c => c.ClientId == apt.ClientId);
                if (client != null)
                {
                    try
                    {
                        await bot.SendMessage(client.TelegramId,
                            $"📅 Ваша запись перенесена.\n\nБыло: {oldWhen}\nСтало: {newDate:dd.MM.yyyy} {newTime:HH:mm}",
                            cancellationToken: ct);
                    }
                    catch { }
                }
            }

            await bot.SendMessage(chatId,
                $"✅ Запись перенесена на {newDate:dd.MM.yyyy} {newTime:HH:mm}. Клиент уведомлён.",
                replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                cancellationToken: ct);
        }

        public static async Task CancelAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            SessionStore.Reset(chatId);
            await bot.SendMessage(chatId, "Перенос отменён.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
        }

        private static async Task StaleAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            SessionStore.Reset(chatId);
            await bot.SendMessage(chatId, "Сессия устарела. Откройте «Записи» снова.", cancellationToken: ct);
        }

        public static bool IsRescheduleState(SessionState state) =>
            state is SessionState.Admin_Reschedule_PickDate
                or SessionState.Admin_Reschedule_PickTime
                or SessionState.Admin_Reschedule_Confirm;
    }
}

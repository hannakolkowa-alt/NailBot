using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ScheduleService
    {
        public static string? LastSlotError { get; private set; }
        public static async Task<List<WorkingDate>> GetWorkingDatesAsync(DateOnly from, DateOnly to)
        {
            var response = await SupabaseConfig.GetClient().From<WorkingDate>().Get();
            return (response.Models ?? new List<WorkingDate>())
                .Where(d => d.Date >= from && d.Date <= to)
                .OrderBy(d => d.Date)
                .ToList();
        }

        public static async Task<List<TimeSlot>> GetSlotsForWorkingDateAsync(Guid workingDateId)
        {
            var slots = await SupabaseConfig.GetClient()
                .From<TimeSlot>()
                .Where(s => s.WorkingDateId == workingDateId)
                .Get();
            return (slots.Models ?? new List<TimeSlot>()).OrderBy(s => s.Time).ToList();
        }

        public static async Task<List<TimeSlot>> GetFreeSlotsAsync(Guid workingDateId)
        {
            var slots = await SupabaseConfig.GetClient()
                .From<TimeSlot>()
                .Where(s => s.WorkingDateId == workingDateId)
                .Get();

            var booked = await GetBookedSlotIdsAsync(workingDateId);

            return (slots.Models ?? new List<TimeSlot>())
                .Where(s => !s.IsBooked && !booked.Contains(s.TimeSlotId))
                .OrderBy(s => s.Time)
                .ToList();
        }

        /// <summary>Свободные слоты для переноса; includeSlotId — текущий слот записи на этой дате.</summary>
        public static async Task<List<TimeSlot>> GetSlotsForRescheduleAsync(
            Guid workingDateId,
            Guid excludeAppointmentId,
            Guid? includeSlotId = null)
        {
            var bookedOthers = await GetBookedSlotIdsAsync(workingDateId, excludeAppointmentId);
            var all = await GetSlotsForWorkingDateAsync(workingDateId);

            return all
                .Where(s =>
                    (!s.IsBooked && !bookedOthers.Contains(s.TimeSlotId))
                    || (includeSlotId.HasValue && s.TimeSlotId == includeSlotId.Value))
                .OrderBy(s => s.Time)
                .ToList();
        }

        public static async Task<bool> SetSlotBookedAsync(Guid timeSlotId, bool isBooked)
        {
            try
            {
                var res = await SupabaseConfig.GetClient()
                    .From<TimeSlot>()
                    .Where(s => s.TimeSlotId == timeSlotId)
                    .Set(s => s.IsBooked, isBooked)
                    .Update();
                return res.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetSlotBookedAsync: {ex.Message}");
                return false;
            }
        }

        public static async Task<HashSet<Guid>> GetBookedSlotIdsAsync(Guid workingDateId, Guid? excludeAppointmentId = null)
        {
            var appts = await SupabaseConfig.GetClient()
                .From<Appointment>()
                .Where(a => a.WorkingDateId == workingDateId)
                .Get();

            var active = (appts.Models ?? new List<Appointment>())
                .Where(a => a.Status is "confirmed" or "active" or "pending" or "approved")
                .Where(a => !excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value)
                .Select(a => a.TimeSlotId)
                .ToHashSet();

            return active;
        }

        public static async Task<WorkingDate?> AddWorkingDateAsync(Guid masterId, DateOnly date)
        {
            var all = await GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
            var found = all.FirstOrDefault(d => d.MasterId == masterId && d.Date == date);
            if (found != null)
                return found;

            var wd = new WorkingDate
            {
                DateId = Guid.NewGuid(),
                MasterId = masterId,
                Date = date
            };
            var res = await SupabaseConfig.GetClient().From<WorkingDate>().Insert(wd);
            return res.Models?.FirstOrDefault();
        }

        public static async Task<TimeSlot?> AddTimeSlotAsync(Guid workingDateId, TimeOnly time)
        {
            LastSlotError = null;
            try
            {
                var existing = await GetFreeSlotsAsync(workingDateId);
                var sameTime = existing.FirstOrDefault(s => s.Time == time);
                if (sameTime != null)
                    return sameTime;

                var allOnDate = await SupabaseConfig.GetClient()
                    .From<TimeSlot>()
                    .Where(s => s.WorkingDateId == workingDateId)
                    .Get();
                var booked = allOnDate.Models?.FirstOrDefault(s => s.Time == time);
                if (booked != null)
                    return booked;

                var slot = new TimeSlot
                {
                    TimeSlotId = Guid.NewGuid(),
                    WorkingDateId = workingDateId,
                    Time = time,
                    IsBooked = false
                };
                var res = await SupabaseConfig.GetClient().From<TimeSlot>().Insert(slot);
                var created = res.Models?.FirstOrDefault();
                if (created != null)
                    return created;

                LastSlotError = "Insert вернул пустой ответ. Выполните supabase_fix_time_slots.sql";
                return null;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("23505", StringComparison.Ordinal) ||
                    ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                {
                    var all = await SupabaseConfig.GetClient()
                        .From<TimeSlot>()
                        .Where(s => s.WorkingDateId == workingDateId)
                        .Get();
                    var dup = all.Models?.FirstOrDefault(s => s.Time == time);
                    if (dup != null)
                        return dup;

                    LastSlotError = "В Supabase удалите ограничение time_slots_время_key — выполните supabase_fix_time_slots.sql";
                }
                else
                {
                    LastSlotError = ex.Message;
                }

                Console.WriteLine($"AddTimeSlot error: {ex}");
                throw;
            }
        }

        public static async Task<string> FormatMonthScheduleAsync(int year, int month)
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var dates = await GetWorkingDatesAsync(from, to);

            if (!dates.Any())
                return $"📅 Расписание на {month:00}.{year}: даты пока не добавлены мастером.";

            var lines = new List<string> { $"📅 Расписание на {month:00}.{year}:\n" };
            foreach (var d in dates)
            {
                var all = await GetSlotsForWorkingDateAsync(d.DateId);
                var free = await GetFreeSlotsAsync(d.DateId);
                lines.Add($"• {d.Date:dd.MM.yyyy} — слотов: {all.Count}, свободно: {free.Count}");
            }
            return string.Join("\n", lines);
        }

        public static async Task<WorkingDate?> GetWorkingDateByDateAsync(Guid masterId, DateOnly date)
        {
            var all = await GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue);
            return all.FirstOrDefault(d => d.MasterId == masterId && d.Date == date);
        }

        public static async Task<HashSet<DateOnly>> GetScheduledDatesInMonthAsync(int year, int month)
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var dates = await GetWorkingDatesAsync(from, to);
            return dates.Select(d => d.Date).ToHashSet();
        }

        public static async Task<List<TimeOnly>> GetSuggestedTimesAsync()
        {
            var defaults = Enumerable.Range(9, 10).Select(h => new TimeOnly(h, 0)).ToList();
            var res = await SupabaseConfig.GetClient().From<TimeSlot>().Get();
            var fromDb = (res.Models ?? new List<TimeSlot>()).Select(s => s.Time);
            return defaults.Concat(fromDb).Distinct().OrderBy(t => t).ToList();
        }

        public static async Task<bool> IsSlotBookedAsync(Guid timeSlotId)
        {
            var appts = await SupabaseConfig.GetClient().From<Appointment>().Get();
            return (appts.Models ?? new List<Appointment>())
                .Any(a => a.TimeSlotId == timeSlotId && IsActiveAppointmentStatus(a.Status));
        }

        private static bool IsActiveAppointmentStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            return s is "confirmed" or "active" or "pending" or "approved" or "completed";
        }

        public static async Task<bool> DeleteTimeSlotAsync(Guid timeSlotId)
        {
            if (await IsSlotBookedAsync(timeSlotId))
                return false;

            await SupabaseConfig.GetClient().From<TimeSlot>().Where(s => s.TimeSlotId == timeSlotId).Delete();
            return true;
        }

        public static async Task<(bool Ok, string? Error)> UpdateTimeSlotAsync(Guid timeSlotId, TimeOnly newTime)
        {
            if (await IsSlotBookedAsync(timeSlotId))
                return (false, "Слот занят записью — изменить время нельзя.");

            var slotRes = await SupabaseConfig.GetClient().From<TimeSlot>().Where(s => s.TimeSlotId == timeSlotId).Get();
            var slot = slotRes.Models?.FirstOrDefault();
            if (slot == null || !slot.WorkingDateId.HasValue)
                return (false, "Слот не найден.");

            var onDate = await GetSlotsForWorkingDateAsync(slot.WorkingDateId.Value);
            if (onDate.Any(s => s.TimeSlotId != timeSlotId && s.Time == newTime))
                return (false, "На эту дату уже есть такое время.");

            try
            {
                var res = await SupabaseConfig.GetClient()
                    .From<TimeSlot>()
                    .Where(s => s.TimeSlotId == timeSlotId)
                    .Set(s => s.Time, newTime)
                    .Update();
                return res.Models?.Count > 0 ? (true, null) : (false, "Не удалось обновить.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool Ok, string? Error)> DeleteWorkingDateAsync(Guid dateId)
        {
            var slots = await GetSlotsForWorkingDateAsync(dateId);
            foreach (var s in slots)
            {
                if (await IsSlotBookedAsync(s.TimeSlotId))
                    return (false, "На эту дату есть записи клиентов — удалите слоты вручную или оставьте день.");
            }

            foreach (var s in slots)
                await SupabaseConfig.GetClient().From<TimeSlot>().Where(x => x.TimeSlotId == s.TimeSlotId).Delete();

            await SupabaseConfig.GetClient().From<WorkingDate>().Where(d => d.DateId == dateId).Delete();
            return (true, null);
        }
    }
}

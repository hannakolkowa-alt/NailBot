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

        public static async Task<HashSet<Guid>> GetBookedSlotIdsAsync(Guid workingDateId)
        {
            var appts = await SupabaseConfig.GetClient()
                .From<Appointment>()
                .Where(a => a.WorkingDateId == workingDateId)
                .Get();

            var active = (appts.Models ?? new List<Appointment>())
                .Where(a => a.Status is "confirmed" or "completed")
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
                var free = await GetFreeSlotsAsync(d.DateId);
                lines.Add($"• {d.Date:dd.MM.yyyy} — свободно слотов: {free.Count}");
            }
            return string.Join("\n", lines);
        }
    }
}

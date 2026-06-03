using TelegramBot.Constants;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class AppointmentService
    {
        public static async Task<List<Appointment>> GetActiveAppointmentsAsync()
        {
            var res = await SupabaseConfig.GetClient().From<Appointment>().Get();
            return (res.Models ?? new List<Appointment>())
                .Where(a => IsActiveStatus(a.Status))
                .OrderByDescending(a => a.AppointmentId)
                .ToList();
        }

        public static async Task<List<Appointment>> GetByClientTelegramIdAsync(long telegramId)
        {
            var client = await ClientService.GetByTelegramIdAsync(telegramId);
            if (client == null) return new List<Appointment>();

            var res = await SupabaseConfig.GetClient().From<Appointment>().Get();
            return (res.Models ?? new List<Appointment>())
                .Where(a => a.ClientId == client.ClientId && IsActiveStatus(a.Status))
                .ToList();
        }

        public static async Task<List<Appointment>> GetCompletedByClientTelegramIdAsync(long telegramId)
        {
            var client = await ClientService.GetByTelegramIdAsync(telegramId);
            if (client == null) return new List<Appointment>();

            var res = await SupabaseConfig.GetClient().From<Appointment>().Get();
            return (res.Models ?? new List<Appointment>())
                .Where(a => a.ClientId == client.ClientId && IsCompletedStatus(a.Status))
                .OrderByDescending(a => a.AppointmentId)
                .ToList();
        }

        public static async Task<Appointment?> GetByIdAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.GetClient().From<Appointment>().Get();
            return res.Models?.FirstOrDefault(a => a.AppointmentId == appointmentId);
        }

        public static async Task<Appointment?> CreateFromRequestAsync(Request request, Guid masterId, Guid workingDateId, Guid timeSlotId)
        {
            foreach (var status in new[] { AppointmentStatus.Confirmed, "confirmed", "CONFIRMED", "active" })
            {
                try
                {
                    var appt = new Appointment
                    {
                        AppointmentId = Guid.NewGuid(),
                        RequestId = request.RequestId,
                        ClientId = request.ClientId,
                        MasterId = masterId,
                        WorkingDateId = workingDateId,
                        TimeSlotId = timeSlotId,
                        Status = status
                    };
                    var res = await SupabaseConfig.GetClient().From<Appointment>().Insert(appt);
                    var created = res.Models?.FirstOrDefault();
                    if (created != null)
                        return created;

                    var all = await SupabaseConfig.GetClient().From<Appointment>().Get();
                    created = all.Models?.FirstOrDefault(a => a.RequestId == request.RequestId);
                    if (created != null)
                        return created;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Appointment insert status={status}: {ex.Message}");
                }
            }
            return null;
        }

        public static async Task<bool> MarkCompletedAsync(Guid appointmentId) =>
            await UpdateStatusAsync(appointmentId, AppointmentStatus.Completed, "completed", "COMPLETED");

        public static async Task<bool> MarkNoShowAsync(Guid appointmentId) =>
            await UpdateStatusAsync(appointmentId, AppointmentStatus.NoShow, "noshow", "NO_SHOW");

        private static async Task<bool> UpdateStatusAsync(Guid appointmentId, params string[] statusCandidates)
        {
            foreach (var status in statusCandidates)
            {
                try
                {
                    var res = await SupabaseConfig.GetClient()
                        .From<Appointment>()
                        .Where(a => a.AppointmentId == appointmentId)
                        .Set(a => a.Status, status)
                        .Update();
                    if (res.Models?.Count > 0)
                        return true;
                }
                catch { }
            }
            return false;
        }

        public static async Task<bool> CancelAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.GetClient()
                .From<Appointment>()
                .Where(a => a.AppointmentId == appointmentId)
                .Set(a => a.Status, AppointmentStatus.Cancelled)
                .Update();
            return res.Models?.Count > 0;
        }

        public static async Task<(bool Ok, string? Error)> RescheduleAsync(
            Guid appointmentId,
            Guid newWorkingDateId,
            Guid newTimeSlotId,
            DateOnly newDate,
            TimeOnly newTime)
        {
            var apt = await GetByIdAsync(appointmentId);
            if (apt == null)
                return (false, "Запись не найдена");

            var oldSlotId = apt.TimeSlotId;

            if (apt.RequestId.HasValue)
            {
                if (!await RequestService.UpdateDesiredScheduleAsync(apt.RequestId.Value, newDate, newTime))
                    return (false, "Не удалось обновить заявку");
            }

            await ScheduleService.SetSlotBookedAsync(oldSlotId, false);
            await ScheduleService.SetSlotBookedAsync(newTimeSlotId, true);

            try
            {
                var res = await SupabaseConfig.GetClient()
                    .From<Appointment>()
                    .Where(a => a.AppointmentId == appointmentId)
                    .Set(a => a.WorkingDateId, newWorkingDateId)
                    .Set(a => a.TimeSlotId, newTimeSlotId)
                    .Update();
                if (res.Models?.Count > 0)
                    return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RescheduleAsync: {ex.Message}");
                return (false, ex.Message);
            }

            return (false, "Не удалось обновить запись");
        }

        private static bool IsActiveStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            return s is "confirmed" or "active" or "pending" or "approved";
        }

        private static bool IsCompletedStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            return s is "completed";
        }

        private static bool IsNoShowStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            return s is "no_show" or "noshow";
        }

        public static bool IsTerminalStatus(string? status) =>
            IsCompletedStatus(status) || IsNoShowStatus(status);
    }
}

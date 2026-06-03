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

        public static async Task<bool> MarkCompletedAsync(Guid appointmentId)
        {
            foreach (var status in new[] { AppointmentStatus.Completed, "completed", "COMPLETED" })
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
    }
}

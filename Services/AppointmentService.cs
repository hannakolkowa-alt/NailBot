using TelegramBot.Constants;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class AppointmentService
    {
        public static async Task<List<Appointment>> GetActiveAppointmentsAsync()
        {
            var res = await SupabaseConfig.Client.From<Appointment>().Get();
            return (res.Models ?? new List<Appointment>())
                .Where(a => a.Status == AppointmentStatus.Confirmed)
                .ToList();
        }

        public static async Task<List<Appointment>> GetByClientTelegramIdAsync(long telegramId)
        {
            var client = await ClientService.GetByTelegramIdAsync(telegramId);
            if (client == null) return new List<Appointment>();

            var res = await SupabaseConfig.Client
                .From<Appointment>()
                .Where(a => a.ClientId == client.ClientId)
                .Get();

            return (res.Models ?? new List<Appointment>())
                .Where(a => a.Status == AppointmentStatus.Confirmed)
                .ToList();
        }

        public static async Task<Appointment?> CreateFromRequestAsync(Request request, Guid masterId, Guid workingDateId, Guid timeSlotId)
        {
            var appt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                RequestId = request.RequestId,
                ClientId = request.ClientId,
                MasterId = masterId,
                WorkingDateId = workingDateId,
                TimeSlotId = timeSlotId,
                Status = AppointmentStatus.Confirmed
            };
            var res = await SupabaseConfig.Client.From<Appointment>().Insert(appt);
            return res.Models?.FirstOrDefault();
        }

        public static async Task<bool> MarkCompletedAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.Client
                .From<Appointment>()
                .Where(a => a.AppointmentId == appointmentId)
                .Set(a => a.Status, AppointmentStatus.Completed)
                .Update();
            return res.Models?.Count > 0;
        }

        public static async Task<bool> CancelAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.Client
                .From<Appointment>()
                .Where(a => a.AppointmentId == appointmentId)
                .Set(a => a.Status, AppointmentStatus.Cancelled)
                .Update();
            return res.Models?.Count > 0;
        }
    }
}

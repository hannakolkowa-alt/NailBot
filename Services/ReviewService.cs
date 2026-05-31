using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ReviewService
    {
        public static async Task<List<Review>> GetAllAsync()
        {
            var res = await SupabaseConfig.GetClient().From<Review>().Get();
            return res.Models ?? new List<Review>();
        }

        public static async Task<bool> AddAsync(Guid clientId, Guid? appointmentId, string text)
        {
            var review = new Review
            {
                ReviewId = Guid.NewGuid(),
                ClientId = clientId,
                AppointmentId = appointmentId,
                Text = text
            };
            var res = await SupabaseConfig.GetClient().From<Review>().Insert(review);
            return res.Models?.Count > 0;
        }

        public static async Task<bool> HasReviewForAppointmentAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.GetClient()
                .From<Review>()
                .Where(r => r.AppointmentId == appointmentId)
                .Get();
            return res.Models?.Any() == true;
        }
    }
}

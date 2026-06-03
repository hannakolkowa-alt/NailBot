using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ReviewService
    {
        public static string FormatStars(int? rating)
        {
            var r = Math.Clamp(rating ?? 5, 1, 5);
            return string.Concat(Enumerable.Repeat("⭐", r)) + string.Concat(Enumerable.Repeat("☆", 5 - r));
        }

        public static async Task<List<Review>> GetAllAsync()
        {
            var res = await SupabaseConfig.GetClient().From<Review>().Get();
            return (res.Models ?? new List<Review>())
                .OrderByDescending(r => r.ReviewId)
                .ToList();
        }

        public static async Task<bool> HasReviewForAppointmentAsync(Guid appointmentId)
        {
            var res = await SupabaseConfig.GetClient()
                .From<Review>()
                .Where(r => r.AppointmentId == appointmentId)
                .Get();
            return res.Models?.Any() == true;
        }

        public static async Task<bool> AddAsync(Guid clientId, Guid? appointmentId, int rating, string text)
        {
            if (appointmentId.HasValue && await HasReviewForAppointmentAsync(appointmentId.Value))
                return false;

            var review = new Review
            {
                ReviewId = Guid.NewGuid(),
                ClientId = clientId,
                AppointmentId = appointmentId,
                Rating = Math.Clamp(rating, 1, 5),
                Text = text.Trim()
            };
            var res = await SupabaseConfig.GetClient().From<Review>().Insert(review);
            return res.Models?.Count > 0;
        }

        public static string FormatPublicReview(Review review, Client? client)
        {
            var name = client?.FirstName;
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(client?.TelegramUsername) ? "Клиент" : $"@{client.TelegramUsername.TrimStart('@')}";

            var stars = FormatStars(review.Rating);
            var body = string.IsNullOrWhiteSpace(review.Text) ? "(без комментария)" : review.Text;
            return $"{stars} {name}\n{body}";
        }
    }
}

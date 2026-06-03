using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class ReviewService
    {
        public static string? LastAddError { get; set; }

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
            LastAddError = null;
            if (appointmentId.HasValue && await HasReviewForAppointmentAsync(appointmentId.Value))
            {
                LastAddError = "Отзыв на этот визит уже оставлен.";
                return false;
            }

            var stars = FormatStars(rating);
            var body = text.Trim();

            try
            {
                var review = new Review
                {
                    ReviewId = Guid.NewGuid(),
                    ClientId = clientId,
                    AppointmentId = appointmentId,
                    Rating = Math.Clamp(rating, 1, 5),
                    Text = body
                };
                var res = await SupabaseConfig.GetClient().From<Review>().Insert(review);
                if (res.Models?.Count > 0)
                    return true;
            }
            catch (Exception ex) when (IsMissingRatingColumn(ex))
            {
                Console.WriteLine($"Review insert without rating column: {ex.Message}");
                try
                {
                    var legacyText = string.IsNullOrEmpty(body)
                        ? $"{stars} (оценка {rating}/5)"
                        : $"{stars} (оценка {rating}/5)\n{body}";

                    var legacy = new ReviewInsertLegacy
                    {
                        ReviewId = Guid.NewGuid(),
                        ClientId = clientId,
                        AppointmentId = appointmentId,
                        Text = legacyText
                    };
                    var res = await SupabaseConfig.GetClient().From<ReviewInsertLegacy>().Insert(legacy);
                    if (res.Models?.Count > 0)
                        return true;
                }
                catch (Exception ex2)
                {
                    LastAddError = ex2.Message;
                    Console.WriteLine($"Review legacy insert: {ex2}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastAddError = ex.Message;
                Console.WriteLine($"Review insert: {ex}");
                return false;
            }

            LastAddError = "Пустой ответ от базы данных.";
            return false;
        }

        public static string FormatPublicReview(Review review, Client? client)
        {
            var name = client?.FirstName;
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(client?.TelegramUsername) ? "Клиент" : $"@{client.TelegramUsername.TrimStart('@')}";

            var stars = FormatStars(review.Rating);
            var body = string.IsNullOrWhiteSpace(review.Text) ? "(без комментария)" : review.Text;
            if (!review.Rating.HasValue && body.Contains('⭐'))
            {
                var nl = body.IndexOf('\n');
                if (nl > 0)
                    return $"{body[..nl]}\n{name}\n{body[(nl + 1)..]}";
            }
            return $"{stars} {name}\n{body}";
        }

        public static string GetClientErrorMessage(string? technicalError)
        {
            if (technicalError != null && IsMissingRatingColumnMessage(technicalError))
                return "Отзыв временно не сохраняется: в базе нет колонки rating.\n\n" +
                       "Владельцу бота: Supabase → SQL → выполнить supabase_reviews_rating.sql → API → Reload schema.";

            if (!string.IsNullOrWhiteSpace(technicalError) && technicalError.Length > 200)
                return "Не удалось сохранить отзыв. Попробуйте позже или напишите мастеру.";

            return string.IsNullOrWhiteSpace(technicalError)
                ? "Не удалось сохранить отзыв. Возможно, он уже был оставлен."
                : $"Не удалось сохранить отзыв.\n{technicalError}";
        }

        private static bool IsMissingRatingColumn(Exception ex) =>
            IsMissingRatingColumnMessage(ex.Message);

        private static bool IsMissingRatingColumnMessage(string message) =>
            message.Contains("PGRST204", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("'rating'", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rating", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("schema cache", StringComparison.OrdinalIgnoreCase);
    }
}

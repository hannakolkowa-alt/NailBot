namespace TelegramBot.Constants
{
    public static class RequestStatus
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Cancelled = "cancelled";

        /// <summary>Текст статуса заявки для сообщений клиенту и мастеру.</summary>
        public static string ToDisplayRussian(string? status)
        {
            return (status ?? "").Trim().ToLowerInvariant() switch
            {
                "pending" or "new" or "created" => "⏳ Ожидает подтверждения",
                "approved" => "✅ Подтверждено",
                "rejected" => "❌ Отклонено",
                "cancelled" => "🚫 Отменено",
                _ => string.IsNullOrWhiteSpace(status) ? "—" : status!
            };
        }
    }

    public static class AppointmentStatus
    {
        public const string Confirmed = "confirmed";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
    }
}

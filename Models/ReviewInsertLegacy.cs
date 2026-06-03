using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    /// <summary>Insert без колонки rating (старая схема БД).</summary>
    [Table("reviews")]
    public class ReviewInsertLegacy : BaseModel
    {
        [PrimaryKey("review_id", false)]
        public Guid ReviewId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("appointment_id")]
        public Guid? AppointmentId { get; set; }

        [Column("text")]
        public string Text { get; set; } = "";
    }
}

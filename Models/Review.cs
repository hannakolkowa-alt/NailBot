using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    [Table("reviews")]
    public class Review : BaseModel
    {
        [PrimaryKey("review_id", false)]
        public Guid ReviewId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("appointment_id")]
        public Guid? AppointmentId { get; set; }

        [Column("rating")]
        public int? Rating { get; set; }

        [Column("text")]
        public string Text { get; set; } = "";
    }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    [Table("services")]
    public class Service : BaseModel
    {
        [PrimaryKey("service_id", false)]
        public Guid ServiceId { get; set; }

        [Column("category_id")]
        public Guid CategoryId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("duration_minutes")]
        public int DurationMinutes { get; set; }

        [Column("price")]
        public decimal Price { get; set; }
    }
}

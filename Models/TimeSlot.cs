using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    [Table("time_slots")]
    public class TimeSlot : BaseModel
    {
        [PrimaryKey("time_slot_id", false)]
        public Guid TimeSlotId { get; set; }

        [Column("время")]
        public TimeOnly Time { get; set; }
    }
}

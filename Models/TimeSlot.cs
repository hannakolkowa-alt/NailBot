using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    [Table("time_slots")]
    public class TimeSlot : BaseModel
    {
        [PrimaryKey("time_slot_id", false)]
        public Guid TimeSlotId { get; set; }

        [Column("time")]
        public TimeOnly Time { get; set; }

        [Column("working_date_id")]
        public Guid? WorkingDateId { get; set; }

        [Column("is_booked")]
        public bool IsBooked { get; set; }
    }
}

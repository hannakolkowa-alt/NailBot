using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot.Models
{
    [Table("appointments")]
    public class Appointment : BaseModel
    {
        [PrimaryKey("appointment_id", false)]
        public Guid AppointmentId { get; set; }

        [Column("request_id")]
        public Guid? RequestId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("master_id")]
        public Guid MasterId { get; set; }

        [Column("working_date_id")]
        public Guid WorkingDateId { get; set; }

        [Column("time_slot_id")]
        public Guid TimeSlotId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "confirmed";
    }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        [Column("text")]
        public string Text { get; set; }
    }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot.Models
{
    [Table("requests")]
    public class Request : BaseModel
    {
        [PrimaryKey("request_id", false)]
        public Guid RequestId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("desired_date")]
        public DateOnly? DesiredDate { get; set; }

        [Column("desired_time")]
        public TimeOnly? DesiredTime { get; set; }

        [Column("comment")]
        public string Comment { get; set; }

        [Column("status")]
        public string Status { get; set; } = "APPROVED";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}

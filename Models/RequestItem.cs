using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot.Models
{
    [Table("request_items")]
    public class RequestItem : BaseModel
    {
        [Column("request_id")]
        public Guid RequestId { get; set; }

        [Column("service_id")]
        public Guid ServiceId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; } = 1;
    }
}

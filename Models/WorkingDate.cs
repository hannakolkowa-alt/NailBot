using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot.Models
{
    [Table("working_dates")]
    public class WorkingDate : BaseModel
    {
        [PrimaryKey("date_id", false)]
        public Guid DateId { get; set; }

        [Column("master_id")]
        public Guid MasterId { get; set; }

        [Column("date")]
        public DateOnly Date { get; set; }
    }
}

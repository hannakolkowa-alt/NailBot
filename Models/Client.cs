using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TelegramBot.Models
{
    [Table("clients")]
    public class Client : BaseModel
    {
        [PrimaryKey("client_id", false)]
        public Guid ClientId { get; set; }

        [Column("telegram_id")]
        public long TelegramId { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("telegram_username")]
        public string TelegramUsername { get; set; }
    }
}

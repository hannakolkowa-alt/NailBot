using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace TelegramBot.Models
{
    [Table("masters")]
    public class Master : BaseModel
    {
        [PrimaryKey("master_id", false)] public Guid MasterId { get; set; }
        [Column("name")] public string Name { get; set; }
        [Column("telegram_username")] public string TelegramUsername { get; set; }
        [Column("experience")] public string Experience { get; set; }
        [Column("description")] public string Description { get; set; }
        [Column("created_at")] public DateTime? CreatedAt { get; set; }
        [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
    }
}
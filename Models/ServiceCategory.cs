using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace TelegramBot.Models
{
    [Table("service_categories")]
    public class ServiceCategory : BaseModel
    {
        [PrimaryKey("category_id", false)] public Guid CategoryId { get; set; }
        [Column("name")] public string Name { get; set; }
    }
}
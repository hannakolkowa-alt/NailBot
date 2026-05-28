using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace TelegramBot.Models
{
    
    [Table("gallery")]
    public class Gallery : BaseModel
    {
        
        [PrimaryKey("photo_id", false)]
        public Guid PhotoId { get; set; }

        
        [Column("master_id")]
        public Guid MasterId { get; set; }

        
        [Column("photo_url")]
        public string PhotoUrl { get; set; }

        
        [Column("description")]
        public string Description { get; set; }

        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
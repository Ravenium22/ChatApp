using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class Room
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        
        public int CreatedById { get; set; }
        
        public User CreatedBy { get; set; } = null!;
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<RoomUser> RoomUsers { get; set; } = new List<RoomUser>();
    }
}
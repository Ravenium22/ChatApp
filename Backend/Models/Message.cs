using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class Message
    {
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        public int SenderId { get; set; }
        public int? RoomId { get; set; }  // null = özel mesaj, dolu = grup mesajı
        public int? ReceiverId { get; set; }  // sadece özel mesajlarda dolu
        
        // Navigation Properties
        public User Sender { get; set; } = null!;
        public Room? Room { get; set; }
        public User? Receiver { get; set; }
        
        // Mesaj durumu
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        
        // Mesaj tipi (metin, resim, dosya vs.)
        public MessageType Type { get; set; } = MessageType.Text;
    }
    
    public enum MessageType
    {
        Text = 0,
        Image = 1,
        File = 2,
        Audio = 3
    }
}
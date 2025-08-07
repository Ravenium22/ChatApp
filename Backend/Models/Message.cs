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
        public int? RoomId { get; set; }  
        public int? ReceiverId { get; set; }  

        public User Sender { get; set; } = null!;
        public Room? Room { get; set; }
        public User? Receiver { get; set; }

        // Mesaj durumu
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        public MessageType Type { get; set; } = MessageType.Text;

        public int? FileAttachmentId { get; set; }
        public FileAttachment? FileAttachment { get; set; }
        
    }

   public enum MessageType
{
    Text = 0,
    Image = 1,
    File = 2,
    Audio = 3,
    Video = 4
}
    
}
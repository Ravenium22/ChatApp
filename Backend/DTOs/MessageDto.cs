using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs
{
    public class SendMessageDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        public int SenderId { get; set; }
        
        public int? RoomId { get; set; }
        public int? ReceiverId { get; set; }
        
        public MessageType Type { get; set; } = MessageType.Text;
    }
    
    public class MessageResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public MessageType Type { get; set; }
        public int? RoomId { get; set; }
        public int? ReceiverId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        
        public UserResponseDto Sender { get; set; } = null!;
        public UserResponseDto? Receiver { get; set; }
    }
    
    public class MarkAsReadDto
    {
        [Required]
        public int MessageId { get; set; }
        
        [Required]
        public int UserId { get; set; }
    }
}
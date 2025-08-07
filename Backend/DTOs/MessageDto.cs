using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs
{
    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty; // Text için Required, file için optional
        
        // SenderId kaldırıldı - JWT token'dan gelecek
        
        public int? RoomId { get; set; }
        public int? ReceiverId { get; set; }
        
        public MessageType Type { get; set; } = MessageType.Text;
        
        // File attachment desteği
        public int? FileAttachmentId { get; set; }
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
        
        // File attachment desteği
        public FileUploadResponseDto? FileAttachment { get; set; }
    }
    
    public class MarkAsReadDto
    {
        [Required]
        public int MessageId { get; set; }
        
        [Required]
        public int UserId { get; set; }
    }
}
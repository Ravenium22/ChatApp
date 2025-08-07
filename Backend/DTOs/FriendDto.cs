using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs
{
    public class SendFriendRequestDto
    {
        [Required]
        [StringLength(50)]
        public string UsernameOrEmail { get; set; } = string.Empty;
    }
    
    public class RespondFriendRequestDto
    {
        [Required]
        public int RequestId { get; set; }
        
        [Required]
        public FriendRequestStatus Response { get; set; } // Accepted or Rejected
    }
    
    public class FriendRequestResponseDto
    {
        public int Id { get; set; }
        public UserResponseDto Sender { get; set; } = null!;
        public UserResponseDto Receiver { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public FriendRequestStatus Status { get; set; }
        public DateTime? RespondedAt { get; set; }
    }
    
    public class FriendResponseDto
    {
        public int Id { get; set; }
        public UserResponseDto Friend { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        
        // Son mesaj bilgisi (WhatsApp tarzı)
        public LastMessageDto? LastMessage { get; set; }
        public int UnreadMessageCount { get; set; }
    }
    
    public class RemoveFriendDto
    {
        [Required]
        public int FriendId { get; set; }
    }
}
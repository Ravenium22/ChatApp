using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class Friendship
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int FriendId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        
        // Navigation Properties
        public User User { get; set; } = null!;
        public User Friend { get; set; } = null!;
    }
    
    public class FriendRequest
    {
        public int Id { get; set; }
        
        [Required]
        public int SenderId { get; set; }
        
        [Required]
        public int ReceiverId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
        public DateTime? RespondedAt { get; set; }
        
        // Navigation Properties
        public User Sender { get; set; } = null!;
        public User Receiver { get; set; } = null!;
    }
    
    public enum FriendRequestStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Cancelled = 3
    }
}
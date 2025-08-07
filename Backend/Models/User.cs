using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; } = false;
        public DateTime? LastSeen { get; set; }

        // Navigation Properties
        public List<Message> SentMessages { get; set; } = new List<Message>();
        public List<Room> CreatedRooms { get; set; } = new List<Room>();
        public List<RoomUser> UserRooms { get; set; } = new List<RoomUser>();
        
        public List<Friendship> Friendships { get; set; } = new List<Friendship>();
        public List<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
        public List<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
    }
}
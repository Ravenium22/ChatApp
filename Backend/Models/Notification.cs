using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class Notification
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
        
        public NotificationType Type { get; set; }
        
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        
        // Ek veriler (JSON format)
        public string? Data { get; set; }
        
        // Navigation Properties
        public User User { get; set; } = null!;
        
        // İlgili entiteler (opsiyonel)
        public int? RelatedUserId { get; set; } // Friend request sender, message sender vs
        public int? RelatedRoomId { get; set; }  // Room invitation vs
        public int? RelatedMessageId { get; set; } // New message notification
        
        public User? RelatedUser { get; set; }
        public Room? RelatedRoom { get; set; }
        public Message? RelatedMessage { get; set; }
    }
    
    public enum NotificationType
    {
        FriendRequest = 0,          // Arkadaş isteği
        FriendRequestAccepted = 1,  // Arkadaş isteği kabul edildi
        NewMessage = 2,             // Yeni mesaj
        RoomInvitation = 3,         // Odaya davet
        RoomMention = 4,            // Oda'da mention
        System = 5                  // Sistem bildirimi
    }
    
    public class NotificationPreferences
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        // In-app notification ayarları
        public bool FriendRequestNotifications { get; set; } = true;
        public bool MessageNotifications { get; set; } = true;
        public bool RoomNotifications { get; set; } = true;
        public bool MentionNotifications { get; set; } = true;
        
        // Email notification ayarları
        public bool EmailFriendRequests { get; set; } = true;
        public bool EmailMessages { get; set; } = false;
        public bool EmailRoomInvitations { get; set; } = true;
        
        // Sound ve display ayarları
        public bool SoundEnabled { get; set; } = true;
        public bool DesktopNotifications { get; set; } = true;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation Property
        public User User { get; set; } = null!;
    }
}
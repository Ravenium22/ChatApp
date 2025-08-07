using Backend.Models;
using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs
{
    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? Data { get; set; }
        
        // Related entities
        public UserResponseDto? RelatedUser { get; set; }
        public RoomResponseDto? RelatedRoom { get; set; }
        public int? RelatedMessageId { get; set; }
        
        // UI için ek bilgiler
        public string TypeDisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
    }
    
    public class CreateNotificationDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
        
        [Required]
        public NotificationType Type { get; set; }
        
        public string? Data { get; set; }
        
        public int? RelatedUserId { get; set; }
        public int? RelatedRoomId { get; set; }
        public int? RelatedMessageId { get; set; }
    }
    
    public class MarkNotificationReadDto
    {
        [Required]
        public int NotificationId { get; set; }
    }
    
    public class NotificationPreferencesDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        
        // In-app notifications
        public bool FriendRequestNotifications { get; set; } = true;
        public bool MessageNotifications { get; set; } = true;
        public bool RoomNotifications { get; set; } = true;
        public bool MentionNotifications { get; set; } = true;
        
        // Email notifications
        public bool EmailFriendRequests { get; set; } = true;
        public bool EmailMessages { get; set; } = false;
        public bool EmailRoomInvitations { get; set; } = true;
        
        // Sound ve display
        public bool SoundEnabled { get; set; } = true;
        public bool DesktopNotifications { get; set; } = true;
        
        public DateTime UpdatedAt { get; set; }
    }
    
    public class UpdateNotificationPreferencesDto
    {
        public bool? FriendRequestNotifications { get; set; }
        public bool? MessageNotifications { get; set; }
        public bool? RoomNotifications { get; set; }
        public bool? MentionNotifications { get; set; }
        public bool? EmailFriendRequests { get; set; }
        public bool? EmailMessages { get; set; }
        public bool? EmailRoomInvitations { get; set; }
        public bool? SoundEnabled { get; set; }
        public bool? DesktopNotifications { get; set; }
    }
}
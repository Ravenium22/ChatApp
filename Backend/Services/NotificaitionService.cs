using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backend.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly IHubContext<ChatHub> _hubContext;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task<Notification> CreateNotificationAsync(CreateNotificationDto dto)
        {
            var notification = new Notification
            {
                UserId = dto.UserId,
                Title = dto.Title,
                Message = dto.Message,
                Type = dto.Type,
                Data = dto.Data,
                RelatedUserId = dto.RelatedUserId,
                RelatedRoomId = dto.RelatedRoomId,
                RelatedMessageId = dto.RelatedMessageId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // 🔥 REAL-TIME NOTIFICATION GÖNDER
            await SendRealtimeNotification(dto.UserId, notification);

            return notification;
        }

        public async Task<List<NotificationResponseDto>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var notifications = await query
                .Include(n => n.RelatedUser)
                .Include(n => n.RelatedRoom)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return notifications.Select(MapToDto).ToList();
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null || notification.IsRead)
                return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        // 🔥 REAL-TIME NOTIFICATION SENDER
        private async Task SendRealtimeNotification(int userId, Notification notification)
        {
            var connectionId = ChatHub.GetUserConnectionId(userId.ToString());
            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceiveNotification", new
                    {
                        notification.Id,
                        notification.Title,
                        notification.Message,
                        notification.Type,
                        notification.CreatedAt,
                        TypeDisplayName = GetTypeDisplayName(notification.Type),
                        Icon = GetTypeIcon(notification.Type)
                    });
            }
        }

        // Specific notification creators
        public async Task CreateFriendRequestNotificationAsync(int receiverId, int senderId, string senderUsername)
        {
            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = receiverId,
                Title = "👥 Friend Request",
                Message = $"{senderUsername} sent you a friend request",
                Type = NotificationType.FriendRequest,
                RelatedUserId = senderId,
                Data = JsonSerializer.Serialize(new { action = "view_friend_requests" })
            });
        }

        public async Task CreateFriendRequestAcceptedNotificationAsync(int senderId, int accepterId, string accepterUsername)
        {
            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = senderId,
                Title = "✅ Friend Request Accepted",
                Message = $"{accepterUsername} accepted your friend request",
                Type = NotificationType.FriendRequestAccepted,
                RelatedUserId = accepterId,
                Data = JsonSerializer.Serialize(new { action = "view_friends" })
            });
        }

        public async Task CreateNewMessageNotificationAsync(int receiverId, int senderId, string senderUsername, string messageContent, int messageId, bool isGroupMessage = false, string? roomName = null)
        {
            var title = isGroupMessage ? $"🏠 New message in {roomName}" : $"💬 New message from {senderUsername}";
            var preview = messageContent.Length > 50 ? messageContent.Substring(0, 50) + "..." : messageContent;

            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = receiverId,
                Title = title,
                Message = preview,
                Type = NotificationType.NewMessage,
                RelatedUserId = senderId,
                RelatedMessageId = messageId,
                Data = JsonSerializer.Serialize(new { action = "open_chat", senderId, isGroupMessage, roomName })
            });
        }

        private NotificationResponseDto MapToDto(Notification notification)
        {
            return new NotificationResponseDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt,
                Data = notification.Data,
                RelatedUser = notification.RelatedUser != null ? new UserResponseDto
                {
                    Id = notification.RelatedUser.Id,
                    Username = notification.RelatedUser.Username,
                    Email = notification.RelatedUser.Email,
                    IsOnline = notification.RelatedUser.IsOnline,
                    LastSeen = notification.RelatedUser.LastSeen
                } : null,
                RelatedMessageId = notification.RelatedMessageId,
                TypeDisplayName = GetTypeDisplayName(notification.Type),
                Icon = GetTypeIcon(notification.Type),
                ActionUrl = GetActionUrl(notification.Type, notification.Data)
            };
        }

        private string GetTypeDisplayName(NotificationType type) => type switch
        {
            NotificationType.FriendRequest => "Friend Request",
            NotificationType.FriendRequestAccepted => "Friend Request Accepted",
            NotificationType.NewMessage => "New Message",
            NotificationType.RoomInvitation => "Room Invitation",
            NotificationType.RoomMention => "Mentioned in Room",
            NotificationType.System => "System Notification",
            _ => "Notification"
        };

        private string GetTypeIcon(NotificationType type) => type switch
        {
            NotificationType.FriendRequest => "👥",
            NotificationType.FriendRequestAccepted => "✅",
            NotificationType.NewMessage => "💬",
            NotificationType.RoomInvitation => "🏠",
            NotificationType.RoomMention => "🔔",
            NotificationType.System => "⚙️",
            _ => "🔔"
        };

        private string GetActionUrl(NotificationType type, string? data)
        {
            return type switch
            {
                NotificationType.FriendRequest => "/friends/requests",
                NotificationType.FriendRequestAccepted => "/friends",
                NotificationType.NewMessage => "/chat",
                NotificationType.RoomInvitation => "/rooms",
                _ => "/notifications"
            };
        }
    }
}
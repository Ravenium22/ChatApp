using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private static readonly Dictionary<string, string> UserConnections = new();

        public ChatHub(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public static string? GetUserConnectionId(string userId)
        {
            return UserConnections.TryGetValue(userId, out string? connectionId) ? connectionId : null;
        }

        public override async Task OnConnectedAsync()
        {
            // Auto-map authenticated user and auto-join their rooms so no manual JoinChat is required
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                UserConnections[userId.ToString()] = Context.ConnectionId;

                // Add this connection to a per-user group for reliable private messages
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User-{userId}");

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    user.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                await Clients.All.SendAsync("UserOnline", userId.ToString());

                // Join all room groups the user is a member of
                var roomIds = await _context.RoomUsers
                    .Where(ru => ru.UserId == userId && ru.IsActive)
                    .Select(ru => ru.RoomId)
                    .ToListAsync();

                foreach (var roomId in roomIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Room-{roomId}");
                }
            }

            await base.OnConnectedAsync();
        }

        public async Task JoinChat(string userId)
        {
            if (!int.TryParse(userId, out int userIdInt))
            {
                await Clients.Caller.SendAsync("Error", "geçersiz user id");
                return;
            }

            // user'ı connection'a map et
            UserConnections[userId] = Context.ConnectionId;

            // Also ensure per-user SignalR group membership
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User-{userIdInt}");

            // kullanıcıyı online yap
            var user = await _context.Users.FindAsync(userIdInt);
            if (user != null)
            {
                user.IsOnline = true;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // tüm kullanıcılara bu kişinin online olduğunu bildir
            await Clients.All.SendAsync("UserOnline", userId);
        }

        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room-{roomId}");
            await Clients.Group($"Room-{roomId}").SendAsync("UserJoinedRoom", Context.ConnectionId, roomId);
        }

        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room-{roomId}");
            await Clients.Group($"Room-{roomId}").SendAsync("UserLeftRoom", Context.ConnectionId, roomId);
        }

        // New method that accepts a message object and extracts user ID from JWT
        public async Task SendMessageWithAuth(SendMessageDto messageDto)
        {
            // Extract user ID from JWT token
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int senderId))
            {
                await Clients.Caller.SendAsync("Error", "Authentication required");
                return;
            }

            // Call the original SendMessage method with extracted user ID
            await SendMessage(
                messageDto.Content ?? string.Empty,
                senderId,
                messageDto.RoomId,
                messageDto.ReceiverId,
                messageDto.FileAttachmentId
            );
        }

        public async Task SendMessage(string content, int senderId, int? roomId, int? receiverId, int? fileAttachmentId = null)
        {
            // room varsa kontrol et
            if (roomId.HasValue)
            {
                var roomExists = await _context.Rooms.AnyAsync(r => r.Id == roomId.Value && r.IsActive);
                if (!roomExists)
                {
                    await Clients.Caller.SendAsync("Error", "room bulunamadı");
                    return;
                }
            }

            // receiver varsa kontrol et
            if (receiverId.HasValue)
            {
                var receiverExists = await _context.Users.AnyAsync(u => u.Id == receiverId.Value);
                if (!receiverExists)
                {
                    await Clients.Caller.SendAsync("Error", "alıcı bulunamadı");
                    return;
                }
            }

            // file attachment varsa kontrol et
            FileAttachment? fileAttachment = null;
            if (fileAttachmentId.HasValue)
            {
                fileAttachment = await _context.FileAttachments
                    .FirstOrDefaultAsync(f => f.Id == fileAttachmentId.Value && f.UploadedById == senderId);
                
                if (fileAttachment == null)
                {
                    await Clients.Caller.SendAsync("Error", "file attachment bulunamadı");
                    return;
                }
            }

            // message type belirle
            var messageType = MessageType.Text;
            if (fileAttachment != null)
            {
                messageType = fileAttachment.FileType switch
                {
                    FileType.Image => MessageType.Image,
                    FileType.Video => MessageType.Video,
                    FileType.Audio => MessageType.Audio,
                    _ => MessageType.File
                };
            }

            // mesajı database'e kaydet
            var message = new Message
            {
                Content = content ?? string.Empty,
                SenderId = senderId,
                RoomId = roomId,
                ReceiverId = receiverId,
                Type = messageType,
                SentAt = DateTime.UtcNow,
                FileAttachmentId = fileAttachmentId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // sender bilgilerini çek
            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null)
            {
                await Clients.Caller.SendAsync("Error", "gönderen bulunamadı");
                return;
            }

            var messageData = new
            {
                message.Id,
                message.Content,
                message.SentAt,
                message.Type,
                message.RoomId,
                message.ReceiverId,
                Sender = new
                {
                    sender.Id,
                    sender.Username
                },
                // file attachment bilgisi ekle
                FileAttachment = fileAttachment != null ? new
                {
                    fileAttachment.Id,
                    fileAttachment.FileName,
                    fileAttachment.OriginalFileName,
                    fileAttachment.ContentType,
                    fileAttachment.FileSize,
                    fileAttachment.FileType,
                    FileUrl = $"http://localhost:5138/{fileAttachment.FilePath}",
                    ThumbnailUrl = !string.IsNullOrEmpty(fileAttachment.ThumbnailPath) 
                        ? $"http://localhost:5138/{fileAttachment.ThumbnailPath}" 
                        : null
                } : null
            };

            // 🔥 NOTIFICATION OLUŞTUR
            if (receiverId.HasValue && receiverId != senderId)
            {
                // Private message notification
                var room = roomId.HasValue ? await _context.Rooms.FindAsync(roomId) : null;
                await _notificationService.CreateNewMessageNotificationAsync(
                    receiverId.Value,
                    senderId,
                    sender.Username,
                    content ?? "File sent",
                    message.Id,
                    roomId.HasValue,
                    room?.Name
                );
            }
            else if (roomId.HasValue)
            {
                var roomMembers = await _context.RoomUsers
                    .Where(ru => ru.RoomId == roomId.Value && ru.IsActive && ru.UserId != senderId)
                    .Select(ru => ru.UserId)
                    .ToListAsync();

                var room = await _context.Rooms.FindAsync(roomId.Value);
                
                foreach (var memberId in roomMembers)
                {
                    await _notificationService.CreateNewMessageNotificationAsync(
                        memberId,
                        senderId,
                        sender.Username,
                        content ?? "File sent",
                        message.Id,
                        true,
                        room?.Name ?? "Room"
                    );
                }
            }

            // eğer grup mesajıysa
            if (roomId.HasValue)
            {
                await Clients.Group($"Room-{roomId}").SendAsync("ReceiveMessage", messageData);
            }
            // eğer özel mesajsa
            else if (receiverId.HasValue)
            {
                var receiverKey = receiverId.Value.ToString();
                if (UserConnections.TryGetValue(receiverKey, out string? receiverConnectionId) && 
                    receiverConnectionId != null)
                {
                    await Clients.Client(receiverConnectionId)
                        .SendAsync("ReceivePrivateMessage", messageData);
                }

                // Also broadcast to per-user groups for reliability (all tabs/devices)
                await Clients.Group($"User-{receiverId.Value}")
                    .SendAsync("ReceivePrivateMessage", messageData);
                await Clients.Group($"User-{senderId}")
                    .SendAsync("ReceivePrivateMessage", messageData);

                // Ensure caller gets it too
                await Clients.Caller.SendAsync("ReceivePrivateMessage", messageData);
            }
        }

        public async Task UserTyping(int roomId, string username)
        {
            await Clients.GroupExcept($"Room-{roomId}", Context.ConnectionId)
                .SendAsync("UserTyping", username);
        }

        public async Task UserStoppedTyping(int roomId, string username)
        {
            await Clients.GroupExcept($"Room-{roomId}", Context.ConnectionId)
                .SendAsync("UserStoppedTyping", username);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // user'ı offline yap ve connection'ı sil
            var userId = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (userId != null)
            {
                UserConnections.Remove(userId);

                if (int.TryParse(userId, out int userIdInt))
                {
                    var user = await _context.Users.FindAsync(userIdInt);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.LastSeen = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    await Clients.All.SendAsync("UserOffline", userId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task NotifyFriendRequest(string receiverUserId, string senderUsername)
        {
            var receiverKey = receiverUserId;
            if (UserConnections.TryGetValue(receiverKey, out string? receiverConnectionId) && receiverConnectionId != null)
            {
                await Clients.Client(receiverConnectionId)
                    .SendAsync("FriendRequestReceived", senderUsername);
            }
        }

        public async Task NotifyFriendRequestResponse(string senderUserId, string response, string responderUsername)
        {
            var senderKey = senderUserId;
            if (UserConnections.TryGetValue(senderKey, out string? senderConnectionId) && 
                senderConnectionId != null)
            {
                await Clients.Client(senderConnectionId)
                    .SendAsync("FriendRequestResponded", response, responderUsername);
            }
        }
    }
}
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;


namespace Backend.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private static readonly Dictionary<string, string> UserConnections = new();

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
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
            // alıcııyı bul
            var receiverKey = receiverUserId;
            if (UserConnections.TryGetValue(receiverKey, out string? receiverConnectionId) && receiverConnectionId != null)
            {
                // alıcıya bildirim gönder
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
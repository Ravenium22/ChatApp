using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Backend.Hubs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly IHubContext<ChatHub> _hubContext;

        public MessageController(ApplicationDbContext context, JwtService jwtService, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _jwtService = jwtService;
            _hubContext = hubContext;
        }

        // GET: api/messages/room/5 - odanın mesajlarını çek
        [HttpGet("room/{roomId}")]
        public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetRoomMessages(int roomId)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            // Kullanıcı bu odanın üyesi mi kontrol et
            var isMember = await _context.RoomUsers
                .AnyAsync(ru => ru.RoomId == roomId && ru.UserId == userId.Value && ru.IsActive);

            if (!isMember)
                return BadRequest("Bu odanın üyesi değilsiniz");

            var messages = await _context.Messages
                .Where(m => m.RoomId == roomId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.FileAttachment) // File desteği
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    Type = m.Type,
                    RoomId = m.RoomId,
                    ReceiverId = m.ReceiverId,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt,
                    Sender = new UserResponseDto
                    {
                        Id = m.Sender.Id,
                        Username = m.Sender.Username,
                        Email = m.Sender.Email,
                        CreatedAt = m.Sender.CreatedAt,
                        IsOnline = m.Sender.IsOnline,
                        LastSeen = m.Sender.LastSeen
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username,
                        Email = m.Receiver.Email,
                        CreatedAt = m.Receiver.CreatedAt,
                        IsOnline = m.Receiver.IsOnline,
                        LastSeen = m.Receiver.LastSeen
                    } : null,
                    // File attachment bilgisi
                    FileAttachment = m.FileAttachment != null ? new FileUploadResponseDto
                    {
                        Id = m.FileAttachment.Id,
                        FileName = m.FileAttachment.FileName,
                        OriginalFileName = m.FileAttachment.OriginalFileName,
                        ContentType = m.FileAttachment.ContentType,
                        FileSize = m.FileAttachment.FileSize,
                        FilePath = m.FileAttachment.FilePath,
                        ThumbnailPath = m.FileAttachment.ThumbnailPath,
                        UploadedAt = m.FileAttachment.UploadedAt,
                        FileType = m.FileAttachment.FileType,
                        FileUrl = $"{Request.Scheme}://{Request.Host}/{m.FileAttachment.FilePath}",
                        ThumbnailUrl = !string.IsNullOrEmpty(m.FileAttachment.ThumbnailPath) 
                            ? $"{Request.Scheme}://{Request.Host}/{m.FileAttachment.ThumbnailPath}" 
                            : null
                    } : null
                })
                .ToListAsync();

            return Ok(messages);
        }

        // GET: api/messages/private/5 - özel mesajları çek (FİXED!)
        [HttpGet("private/{otherUserId}")]
        public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetPrivateMessages(int otherUserId)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            // Diğer kullanıcı var mı kontrol et
            var otherUserExists = await _context.Users.AnyAsync(u => u.Id == otherUserId);
            if (!otherUserExists)
                return NotFound("Kullanıcı bulunamadı");

            // TODO: Arkadaş kontrolü - şimdilik kapalı (test için)
            // var areFriends = await _context.Friendships
            //     .AnyAsync(f => (f.UserId == userId.Value && f.FriendId == otherUserId) ||
            //               (f.UserId == otherUserId && f.FriendId == userId.Value));

            // if (!areFriends)
            //     return BadRequest("Bu kişiyle mesajlaşabilmek için arkadaş olmanız gerekir");

            var messages = await _context.Messages
                .Where(m => m.RoomId == null && 
                           ((m.SenderId == userId.Value && m.ReceiverId == otherUserId) ||
                            (m.SenderId == otherUserId && m.ReceiverId == userId.Value)))
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.FileAttachment) // File desteği
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    Type = m.Type,
                    RoomId = m.RoomId,
                    ReceiverId = m.ReceiverId,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt,
                    Sender = new UserResponseDto
                    {
                        Id = m.Sender.Id,
                        Username = m.Sender.Username,
                        Email = m.Sender.Email,
                        CreatedAt = m.Sender.CreatedAt,
                        IsOnline = m.Sender.IsOnline,
                        LastSeen = m.Sender.LastSeen
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username,
                        Email = m.Receiver.Email,
                        CreatedAt = m.Receiver.CreatedAt,
                        IsOnline = m.Receiver.IsOnline,
                        LastSeen = m.Receiver.LastSeen
                    } : null
                })
                .ToListAsync();

            return Ok(messages);
        }

        // POST: api/messages - mesaj gönder (FİXED!)
        [HttpPost]
        public async Task<ActionResult<MessageResponseDto>> SendMessage([FromBody] SendMessageDto request)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var actualSenderId = userId.Value;

            if (request.Type == MessageType.Text && string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Text mesaj için content gerekli");

            FileAttachment? fileAttachment = null;
            if (request.FileAttachmentId.HasValue)
            {
                fileAttachment = await _context.FileAttachments
                    .FirstOrDefaultAsync(f => f.Id == request.FileAttachmentId.Value && 
                                             f.UploadedById == actualSenderId);
                
                if (fileAttachment == null)
                    return BadRequest("File attachment bulunamadı veya size ait değil");
            }

            if (request.RoomId.HasValue)
            {
                var isMember = await _context.RoomUsers
                    .AnyAsync(ru => ru.RoomId == request.RoomId && ru.UserId == actualSenderId && ru.IsActive);

                if (!isMember)
                    return BadRequest("Bu odaya mesaj gönderme yetkiniz yok");
            }
            else if (request.ReceiverId.HasValue)
            {
                var receiverExists = await _context.Users.AnyAsync(u => u.Id == request.ReceiverId);
                if (!receiverExists)
                    return NotFound("Alıcı bulunamadı");
            }
            else
            {
                return BadRequest("Room ID veya Receiver ID belirtilmeli");
            }

            var message = new Message
            {
                Content = request.Content ?? string.Empty,
                SenderId = actualSenderId,
                RoomId = request.RoomId,
                ReceiverId = request.ReceiverId,
                Type = request.Type,
                SentAt = DateTime.UtcNow,
                FileAttachmentId = request.FileAttachmentId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var savedMessage = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.FileAttachment)
                .Where(m => m.Id == message.Id)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    Type = m.Type,
                    RoomId = m.RoomId,
                    ReceiverId = m.ReceiverId,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt,
                    Sender = new UserResponseDto
                    {
                        Id = m.Sender.Id,
                        Username = m.Sender.Username,
                        Email = m.Sender.Email,
                        CreatedAt = m.Sender.CreatedAt,
                        IsOnline = m.Sender.IsOnline,
                        LastSeen = m.Sender.LastSeen
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username,
                        Email = m.Receiver.Email,
                        CreatedAt = m.Receiver.CreatedAt,
                        IsOnline = m.Receiver.IsOnline,
                        LastSeen = m.Receiver.LastSeen
                    } : null,
                    FileAttachment = m.FileAttachment != null ? new FileUploadResponseDto
                    {
                        Id = m.FileAttachment.Id,
                        FileName = m.FileAttachment.FileName,
                        OriginalFileName = m.FileAttachment.OriginalFileName,
                        ContentType = m.FileAttachment.ContentType,
                        FileSize = m.FileAttachment.FileSize,
                        FilePath = m.FileAttachment.FilePath,
                        ThumbnailPath = m.FileAttachment.ThumbnailPath,
                        UploadedAt = m.FileAttachment.UploadedAt,
                        FileType = m.FileAttachment.FileType,
                        FileUrl = $"{Request.Scheme}://{Request.Host}/{m.FileAttachment.FilePath}",
                        ThumbnailUrl = !string.IsNullOrEmpty(m.FileAttachment.ThumbnailPath) 
                            ? $"{Request.Scheme}://{Request.Host}/{m.FileAttachment.ThumbnailPath}" 
                            : null
                    } : null
                })
                .FirstAsync();

            // NEW: Broadcast over SignalR so others receive in real time
            if (message.RoomId.HasValue)
            {
                await _hubContext.Clients.Group($"Room-{message.RoomId.Value}")
                    .SendAsync("ReceiveMessage", savedMessage);
            }
            else if (message.ReceiverId.HasValue)
            {
                // To receiver: per-user group
                await _hubContext.Clients.Group($"User-{message.ReceiverId.Value}")
                    .SendAsync("ReceivePrivateMessage", savedMessage);
                // To receiver: direct connection if mapped
                var receiverConn = ChatHub.GetUserConnectionId(message.ReceiverId.Value.ToString());
                if (!string.IsNullOrEmpty(receiverConn))
                {
                    await _hubContext.Clients.Client(receiverConn)
                        .SendAsync("ReceivePrivateMessage", savedMessage);
                }
                // To sender: keep other tabs/devices in sync
                await _hubContext.Clients.Group($"User-{message.SenderId}")
                    .SendAsync("ReceivePrivateMessage", savedMessage);
            }

            if (message.RoomId.HasValue)
            {
                return CreatedAtAction(nameof(GetRoomMessages), new { roomId = message.RoomId }, savedMessage);
            }
            else
            {
                return Ok(savedMessage);
            }
        }

        // PUT: api/messages/mark-read - mesajı okundu olarak işaretle
        [HttpPut("mark-read")]
        public async Task<ActionResult> MarkAsRead([FromBody] MarkAsReadDto request)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var message = await _context.Messages.FindAsync(request.MessageId);
            
            if (message == null)
            {
                return NotFound();
            }

            // Sadece alıcı mesajı okundu olarak işaretleyebilir
            if (message.ReceiverId != userId.Value)
            {
                return BadRequest("Bu mesajı okuma yetkisiz yok");
            }

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
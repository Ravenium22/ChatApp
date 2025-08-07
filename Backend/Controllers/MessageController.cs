using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public MessageController(ApplicationDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
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

            // SenderId'yi JWT token'dan al (güvenlik için)
            var actualSenderId = userId.Value;

            // Content validation - text mesaj için required, file mesaj için optional
            if (request.Type == MessageType.Text && string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Text mesaj için content gerekli");

            // File attachment kontrolü
            FileAttachment? fileAttachment = null;
            if (request.FileAttachmentId.HasValue)
            {
                fileAttachment = await _context.FileAttachments
                    .FirstOrDefaultAsync(f => f.Id == request.FileAttachmentId.Value && 
                                             f.UploadedById == actualSenderId);
                
                if (fileAttachment == null)
                    return BadRequest("File attachment bulunamadı veya size ait değil");
            }

            // Room mesajı ise
            if (request.RoomId.HasValue)
            {
                // Room var mı ve kullanıcı üye mi kontrol et
                var isMember = await _context.RoomUsers
                    .AnyAsync(ru => ru.RoomId == request.RoomId && ru.UserId == actualSenderId && ru.IsActive);

                if (!isMember)
                    return BadRequest("Bu odaya mesaj gönderme yetkiniz yok");
            }
            // Private mesaj ise
            else if (request.ReceiverId.HasValue)
            {
                // Receiver var mı kontrol et
                var receiverExists = await _context.Users.AnyAsync(u => u.Id == request.ReceiverId);
                if (!receiverExists)
                    return NotFound("Alıcı bulunamadı");

                // TODO: Arkadaş kontrolü - şimdilik kapalı (test için)
                // var areFriends = await _context.Friendships
                //     .AnyAsync(f => (f.UserId == actualSenderId && f.FriendId == request.ReceiverId) ||
                //                   (f.UserId == request.ReceiverId && f.FriendId == actualSenderId));

                // if (!areFriends)
                //     return BadRequest("Bu kişiye mesaj gönderebilmek için arkadaş olmanız gerekir");
            }
            else
            {
                return BadRequest("Room ID veya Receiver ID belirtilmeli");
            }

            var message = new Message
            {
                Content = request.Content ?? string.Empty,
                SenderId = actualSenderId, // JWT'den gelen ID'yi kullan
                RoomId = request.RoomId,
                ReceiverId = request.ReceiverId,
                Type = request.Type,
                SentAt = DateTime.UtcNow,
                FileAttachmentId = request.FileAttachmentId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // mesajı sender bilgileriyle birlikte return et
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

            // Room mesajı ise CreatedAtAction, private mesaj ise Ok() döndür
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
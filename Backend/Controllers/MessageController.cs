using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MessageController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/messages/room/5 - odanın mesajlarını çek
        [HttpGet("room/{roomId}")]
        public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetRoomMessages(int roomId)
        {
            var messages = await _context.Messages
                .Where(m => m.RoomId == roomId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
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
                        Username = m.Sender.Username
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username
                    } : null
                })
                .ToListAsync();

            return Ok(messages);
        }

        // GET: api/messages/private/5 - özel mesajları çek
        [HttpGet("private/{userId}")]
        public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetPrivateMessages(int userId, [FromQuery] int otherUserId)
        {
            var messages = await _context.Messages
                .Where(m => m.RoomId == null && 
                           ((m.SenderId == userId && m.ReceiverId == otherUserId) ||
                            (m.SenderId == otherUserId && m.ReceiverId == userId)))
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
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
                        Username = m.Sender.Username
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username
                    } : null
                })
                .ToListAsync();

            return Ok(messages);
        }

        // POST: api/messages - mesaj gönder (api üzerinden)
        [HttpPost]
        public async Task<ActionResult<MessageResponseDto>> SendMessage([FromBody] SendMessageDto request)
        {
            var message = new Message
            {
                Content = request.Content,
                SenderId = request.SenderId,
                RoomId = request.RoomId,
                ReceiverId = request.ReceiverId,
                Type = request.Type,
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // mesajı sender bilgileriyle birlikte return et
            var savedMessage = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
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
                        Username = m.Sender.Username
                    },
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        Username = m.Receiver.Username
                    } : null
                })
                .FirstAsync();

            return CreatedAtAction(nameof(GetRoomMessages), new { roomId = message.RoomId }, savedMessage);
        }

        // PUT: api/messages/mark-read - mesajı okundu olarak işaretle
        [HttpPut("mark-read")]
        public async Task<ActionResult> MarkAsRead([FromBody] MarkAsReadDto request)
        {
            var message = await _context.Messages.FindAsync(request.MessageId);
            
            if (message == null)
            {
                return NotFound();
            }

            // Sadece alıcı mesajı okundu olarak işaretleyebilir
            if (message.ReceiverId != request.UserId)
            {
                return Forbid();
            }

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
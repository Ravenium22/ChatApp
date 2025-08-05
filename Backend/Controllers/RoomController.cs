using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/rooms - tüm odaları listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoomResponseDto>>> GetRooms()
        {
            var rooms = await _context.Rooms
                .Where(r => r.IsActive)
                .Include(r => r.CreatedBy)
                .Include(r => r.RoomUsers.Where(ru => ru.IsActive))
                .ThenInclude(ru => ru.User)
                .Select(r => new RoomResponseDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt,
                    IsActive = r.IsActive,
                    CreatedBy = new UserResponseDto
                    {
                        Id = r.CreatedBy.Id,
                        Username = r.CreatedBy.Username,
                        Email = r.CreatedBy.Email,
                        CreatedAt = r.CreatedBy.CreatedAt,
                        IsOnline = r.CreatedBy.IsOnline,
                        LastSeen = r.CreatedBy.LastSeen
                    },
                    MemberCount = r.RoomUsers.Count(ru => ru.IsActive),
                    Members = r.RoomUsers
                        .Where(ru => ru.IsActive)
                        .Select(ru => new RoomMemberDto
                        {
                            Id = ru.User.Id,
                            Username = ru.User.Username,
                            Role = ru.Role,
                            JoinedAt = ru.JoinedAt,
                            IsOnline = ru.User.IsOnline
                        }).ToList(),
                    LastMessage = _context.Messages
                        .Where(m => m.RoomId == r.Id)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new LastMessageDto
                        {
                            Content = m.Content,
                            SentAt = m.SentAt,
                            SenderName = m.Sender.Username
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(rooms);
        }

        // GET: api/rooms/5 - tek oda detayı
        [HttpGet("{id}")]
        public async Task<ActionResult<RoomResponseDto>> GetRoom(int id)
        {
            var room = await _context.Rooms
                .Where(r => r.Id == id && r.IsActive)
                .Include(r => r.CreatedBy)
                .Include(r => r.RoomUsers.Where(ru => ru.IsActive))
                .ThenInclude(ru => ru.User)
                .Select(r => new RoomResponseDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt,
                    IsActive = r.IsActive,
                    CreatedBy = new UserResponseDto
                    {
                        Id = r.CreatedBy.Id,
                        Username = r.CreatedBy.Username,
                        Email = r.CreatedBy.Email,
                        CreatedAt = r.CreatedBy.CreatedAt,
                        IsOnline = r.CreatedBy.IsOnline,
                        LastSeen = r.CreatedBy.LastSeen
                    },
                    MemberCount = r.RoomUsers.Count(ru => ru.IsActive),
                    Members = r.RoomUsers
                        .Where(ru => ru.IsActive)
                        .Select(ru => new RoomMemberDto
                        {
                            Id = ru.User.Id,
                            Username = ru.User.Username,
                            Role = ru.Role,
                            JoinedAt = ru.JoinedAt,
                            IsOnline = ru.User.IsOnline
                        }).ToList(),
                    LastMessage = _context.Messages
                        .Where(m => m.RoomId == r.Id)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new LastMessageDto
                        {
                            Content = m.Content,
                            SentAt = m.SentAt,
                            SenderName = m.Sender.Username
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (room == null)
                return NotFound();

            return Ok(room);
        }

        // POST: api/rooms - yeni oda oluştur
        [HttpPost]
        public async Task<ActionResult<RoomResponseDto>> CreateRoom([FromBody] CreateRoomDto request)
        {
            var room = new Room
            {
                Name = request.Name,
                Description = request.Description,
                CreatedById = request.CreatedById,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            // odayı oluşturan kişiyi otomatik owner yap
            var roomUser = new RoomUser
            {
                RoomId = room.Id,
                UserId = request.CreatedById,
                Role = RoomRole.Owner,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };

            _context.RoomUsers.Add(roomUser);
            await _context.SaveChangesAsync();

            // Get the created room with all necessary data
            var createdRoom = await _context.Rooms
                .Where(r => r.Id == room.Id)
                .Include(r => r.CreatedBy)
                .Select(r => new RoomResponseDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt,
                    IsActive = r.IsActive,
                    CreatedBy = new UserResponseDto
                    {
                        Id = r.CreatedBy.Id,
                        Username = r.CreatedBy.Username,
                        Email = r.CreatedBy.Email,
                        CreatedAt = r.CreatedBy.CreatedAt,
                        IsOnline = r.CreatedBy.IsOnline,
                        LastSeen = r.CreatedBy.LastSeen
                    },
                    MemberCount = 1,
                    Members = new List<RoomMemberDto>(),
                    LastMessage = null
                })
                .FirstOrDefaultAsync();

            return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, createdRoom);
        }

        // POST: api/rooms/5/join - odaya katıl
        [HttpPost("{roomId}/join")]
        public async Task<ActionResult> JoinRoom(int roomId, [FromBody] JoinRoomDto request)
        {
            // oda var mı kontrol et
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null || !room.IsActive)
                return NotFound();

            // kullanıcı zaten üye mi kontrol et
            var existingMember = await _context.RoomUsers
                .FirstOrDefaultAsync(ru => ru.RoomId == roomId && ru.UserId == request.UserId);

            if (existingMember != null)
            {
                if (existingMember.IsActive)
                    return BadRequest("zaten bu odanın üyesisin");

                // eski üye tekrar katılıyor
                existingMember.IsActive = true;
                existingMember.LeftAt = null;
                existingMember.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                // yeni üye
                var roomUser = new RoomUser
                {
                    RoomId = roomId,
                    UserId = request.UserId,
                    Role = RoomRole.Member,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };

                _context.RoomUsers.Add(roomUser);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST: api/rooms/5/leave - odadan ayrıl
        [HttpPost("{roomId}/leave")]
        public async Task<ActionResult> LeaveRoom(int roomId, [FromBody] LeaveRoomDto request)
        {
            var roomUser = await _context.RoomUsers
                .FirstOrDefaultAsync(ru => ru.RoomId == roomId && ru.UserId == request.UserId && ru.IsActive);

            if (roomUser == null)
                return NotFound("bu odanın üyesi değilsin");

            roomUser.IsActive = false;
            roomUser.LeftAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: api/rooms/user/5 - kullanıcının katıldığı odalar
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<UserRoomDto>>> GetUserRooms(int userId)
        {
            var userRooms = await _context.RoomUsers
                .Where(ru => ru.UserId == userId && ru.IsActive)
                .Include(ru => ru.Room)
                .ThenInclude(r => r.CreatedBy)
                .Where(ru => ru.Room.IsActive)
                .Select(ru => new UserRoomDto
                {
                    Id = ru.Room.Id,
                    Name = ru.Room.Name,
                    Description = ru.Room.Description,
                    Role = ru.Role,
                    JoinedAt = ru.JoinedAt,
                    CreatedBy = new UserResponseDto
                    {
                        Id = ru.Room.CreatedBy.Id,
                        Username = ru.Room.CreatedBy.Username,
                        Email = ru.Room.CreatedBy.Email,
                        CreatedAt = ru.Room.CreatedBy.CreatedAt,
                        IsOnline = ru.Room.CreatedBy.IsOnline,
                        LastSeen = ru.Room.CreatedBy.LastSeen
                    },
                    LastMessage = _context.Messages
                        .Where(m => m.RoomId == ru.Room.Id)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new LastMessageDto
                        {
                            Content = m.Content,
                            SentAt = m.SentAt,
                            SenderName = m.Sender.Username
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(userRooms);
        }
    }
}
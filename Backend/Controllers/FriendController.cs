using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Services;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FriendController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public FriendController(ApplicationDbContext context, JwtService jwtService, NotificationService notificationService)
        {
            _context = context;
            _jwtService = jwtService;
            _notificationService = notificationService;
        }

        // POST: api/friend/send-request - arkadaş isteği gönder
        [HttpPost("send-request")]
        public async Task<ActionResult> SendFriendRequest([FromBody] SendFriendRequestDto request)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            // Alıcı kullanıcıyı bul
            var receiver = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || 
                                         u.Email == request.UsernameOrEmail);

            if (receiver == null)
                return NotFound("Kullanıcı bulunamadı");

            if (receiver.Id == userId.Value)
                return BadRequest("Kendine arkadaş isteği gönderemezsin");

            // Zaten arkadaş mı kontrol et
            var existingFriendship = await _context.Friendships
                .AnyAsync(f => (f.UserId == userId.Value && f.FriendId == receiver.Id) ||
                              (f.UserId == receiver.Id && f.FriendId == userId.Value));

            if (existingFriendship)
                return BadRequest("Bu kişi zaten arkadaşın");

            // Bekleyen istek var mı kontrol et
            var existingRequest = await _context.FriendRequests
                .AnyAsync(fr => (fr.SenderId == userId.Value && fr.ReceiverId == receiver.Id ||
                                fr.SenderId == receiver.Id && fr.ReceiverId == userId.Value) &&
                               fr.Status == FriendRequestStatus.Pending);

            if (existingRequest)
                return BadRequest("Bu kişiyle bekleyen bir arkadaş isteği var");

            // Yeni arkadaş isteği oluştur
            var friendRequest = new FriendRequest
            {
                SenderId = userId.Value,
                ReceiverId = receiver.Id,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.FriendRequests.Add(friendRequest);
            await _context.SaveChangesAsync();


            return Ok(new { message = "Arkadaş isteği gönderildi" });
        }

        // GET: api/friend/requests/received - gelen arkadaş istekleri
        [HttpGet("requests/received")]
        public async Task<ActionResult<IEnumerable<FriendRequestResponseDto>>> GetReceivedFriendRequests()
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var requests = await _context.FriendRequests
                .Where(fr => fr.ReceiverId == userId.Value && fr.Status == FriendRequestStatus.Pending)
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Select(fr => new FriendRequestResponseDto
                {
                    Id = fr.Id,
                    Sender = new UserResponseDto
                    {
                        Id = fr.Sender.Id,
                        Username = fr.Sender.Username,
                        Email = fr.Sender.Email,
                        IsOnline = fr.Sender.IsOnline,
                        LastSeen = fr.Sender.LastSeen
                    },
                    Receiver = new UserResponseDto
                    {
                        Id = fr.Receiver.Id,
                        Username = fr.Receiver.Username,
                        Email = fr.Receiver.Email,
                        IsOnline = fr.Receiver.IsOnline,
                        LastSeen = fr.Receiver.LastSeen
                    },
                    CreatedAt = fr.CreatedAt,
                    Status = fr.Status,
                    RespondedAt = fr.RespondedAt
                })
                .ToListAsync();

            return Ok(requests);
        }

        // GET: api/friend/requests/sent - gönderilen arkadaş istekleri
        [HttpGet("requests/sent")]
        public async Task<ActionResult<IEnumerable<FriendRequestResponseDto>>> GetSentFriendRequests()
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var requests = await _context.FriendRequests
                .Where(fr => fr.SenderId == userId.Value && fr.Status == FriendRequestStatus.Pending)
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Select(fr => new FriendRequestResponseDto
                {
                    Id = fr.Id,
                    Sender = new UserResponseDto
                    {
                        Id = fr.Sender.Id,
                        Username = fr.Sender.Username,
                        Email = fr.Sender.Email,
                        IsOnline = fr.Sender.IsOnline,
                        LastSeen = fr.Sender.LastSeen
                    },
                    Receiver = new UserResponseDto
                    {
                        Id = fr.Receiver.Id,
                        Username = fr.Receiver.Username,
                        Email = fr.Receiver.Email,
                        IsOnline = fr.Receiver.IsOnline,
                        LastSeen = fr.Receiver.LastSeen
                    },
                    CreatedAt = fr.CreatedAt,
                    Status = fr.Status,
                    RespondedAt = fr.RespondedAt
                })
                .ToListAsync();

            return Ok(requests);
        }

        // POST: api/friend/respond - arkadaş isteğini yanıtla
        // POST: api/friend/respond - arkadaş isteğini yanıtla
[HttpPost("respond")]
public async Task<ActionResult> RespondToFriendRequest([FromBody] RespondFriendRequestDto request)
{
    var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
    if (!userId.HasValue)
        return Unauthorized();

    var friendRequest = await _context.FriendRequests
        .Include(fr => fr.Sender) // BUNU EKLE
        .FirstOrDefaultAsync(fr => fr.Id == request.RequestId && 
                                  fr.ReceiverId == userId.Value &&
                                  fr.Status == FriendRequestStatus.Pending);

    if (friendRequest == null)
        return NotFound("Arkadaş isteği bulunamadı");

    if (request.Response != FriendRequestStatus.Accepted && 
        request.Response != FriendRequestStatus.Rejected)
        return BadRequest("Geçersiz yanıt");

    // İsteği güncelle
    friendRequest.Status = request.Response;
    friendRequest.RespondedAt = DateTime.UtcNow;

    // Eğer kabul edildiyse, arkadaşlık oluştur
    if (request.Response == FriendRequestStatus.Accepted)
    {
        // İki yönlü arkadaşlık oluştur
        var friendship1 = new Friendship
        {
            UserId = friendRequest.SenderId,
            FriendId = friendRequest.ReceiverId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var friendship2 = new Friendship
        {
            UserId = friendRequest.ReceiverId,
            FriendId = friendRequest.SenderId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Friendships.AddRange(friendship1, friendship2);
    }

    await _context.SaveChangesAsync();

    // 🔥 NOTIFICATION OLUŞTUR (SADECE KABUL EDİLDİĞİNDE)
    if (request.Response == FriendRequestStatus.Accepted)
    {
        var accepter = await _context.Users.FindAsync(userId.Value);
        if (accepter != null)
        {
            await _notificationService.CreateFriendRequestAcceptedNotificationAsync(
                friendRequest.SenderId, // İsteği gönderen kişiye bildirim
                userId.Value,           // Kabul eden kişi
                accepter.Username       // Kabul eden kişinin adı
            );
        }
    }

    var message = request.Response == FriendRequestStatus.Accepted 
        ? "Arkadaş isteği kabul edildi" 
        : "Arkadaş isteği reddedildi";

    return Ok(new { message });
}

        // GET: api/friend/list - arkadaş listesi
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<FriendResponseDto>>> GetFriends()
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var friends = await _context.Friendships
                .Where(f => f.UserId == userId.Value && f.IsActive)
                .Include(f => f.Friend)
                .Select(f => new FriendResponseDto
                {
                    Id = f.Id,
                    Friend = new UserResponseDto
                    {
                        Id = f.Friend.Id,
                        Username = f.Friend.Username,
                        Email = f.Friend.Email,
                        IsOnline = f.Friend.IsOnline,
                        LastSeen = f.Friend.LastSeen,
                        CreatedAt = f.Friend.CreatedAt
                    },
                    CreatedAt = f.CreatedAt,
                    IsOnline = f.Friend.IsOnline,
                    LastSeen = f.Friend.LastSeen,
                    // Son mesajı al (private mesajlar)
                    LastMessage = _context.Messages
                        .Where(m => m.RoomId == null && 
                                   ((m.SenderId == userId.Value && m.ReceiverId == f.FriendId) ||
                                    (m.SenderId == f.FriendId && m.ReceiverId == userId.Value)))
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new LastMessageDto
                        {
                            Content = m.Content,
                            SentAt = m.SentAt,
                            SenderName = m.Sender.Username
                        })
                        .FirstOrDefault(),
                    // Okunmamış mesaj sayısı
                    UnreadMessageCount = _context.Messages
                        .Count(m => m.SenderId == f.FriendId && 
                                   m.ReceiverId == userId.Value && 
                                   m.RoomId == null && 
                                   !m.IsRead)
                })
                .ToListAsync();

            return Ok(friends);
        }

        // DELETE: api/friend/remove - arkadaş sil
        [HttpDelete("remove")]
        public async Task<ActionResult> RemoveFriend([FromBody] RemoveFriendDto request)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            // İki yönlü arkadaşlığı bul
            var friendships = await _context.Friendships
                .Where(f => (f.UserId == userId.Value && f.FriendId == request.FriendId) ||
                           (f.UserId == request.FriendId && f.FriendId == userId.Value))
                .ToListAsync();

            if (!friendships.Any())
                return NotFound("Arkadaşlık bulunamadı");

            // Arkadaşlıkları pasif yap (sil yerine)
            foreach (var friendship in friendships)
            {
                friendship.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Arkadaş silindi" });
        }

        // POST: api/friend/cancel-request - gönderilen isteği iptal et
        [HttpPost("cancel-request")]
        public async Task<ActionResult> CancelFriendRequest([FromBody] RemoveFriendDto request)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var friendRequest = await _context.FriendRequests
                .FirstOrDefaultAsync(fr => fr.SenderId == userId.Value && 
                                          fr.ReceiverId == request.FriendId &&
                                          fr.Status == FriendRequestStatus.Pending);

            if (friendRequest == null)
                return NotFound("Bekleyen arkadaş isteği bulunamadı");

            friendRequest.Status = FriendRequestStatus.Cancelled;
            friendRequest.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Arkadaş isteği iptal edildi" });
        }
    }
}
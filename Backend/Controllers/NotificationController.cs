using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Services;
using Backend.DTOs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private readonly JwtService _jwtService;

        public NotificationController(NotificationService notificationService, JwtService jwtService)
        {
            _notificationService = notificationService;
            _jwtService = jwtService;
        }

        // GET: api/notification - kullanıcının bildirimlerini getir
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationResponseDto>>> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId.Value, unreadOnly, limit);
            return Ok(notifications);
        }

        // GET: api/notification/unread-count - okunmamış bildirim sayısı
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId.Value);
            return Ok(count);
        }

        // PUT: api/notification/{id}/read - bildirimi okundu olarak işaretle
        [HttpPut("{id}/read")]
        public async Task<ActionResult> MarkAsRead(int id)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var success = await _notificationService.MarkAsReadAsync(id, userId.Value);
            if (!success)
                return NotFound("Notification not found or already read");

            return Ok();
        }

        // PUT: api/notification/mark-all-read - tüm bildirimleri okundu olarak işaretle
        [HttpPut("mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId.Value);
            return Ok();
        }

        // POST: api/notification/test - test bildirimi oluştur (development için)
        [HttpPost("test")]
        public async Task<ActionResult> CreateTestNotification([FromBody] string message = "This is a test notification")
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            var notification = await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId.Value,
                Title = "🧪 Test Notification",
                Message = message,
                Type = Models.NotificationType.System
            });

            return Ok(new { message = "Test notification created", notificationId = notification.Id });
        }
    }
}
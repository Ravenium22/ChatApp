using System.Net;
using System.Text.Json;

namespace Backend.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        // Constructor - dependency injection
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next; // sonraki middleware'e geçmek için
            _logger = logger; // hataları loglama için
        }

        // Her HTTP request'te çalışır
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Normal akışı devam ettir
                await _next(context);
            }
            catch (Exception ex)
            {
                // Hata oldu! Logla ve handle et
                _logger.LogError(ex, "Beklenmeyen hata oluştu");
                await HandleExceptionAsync(context, ex);
            }
        }

        // Hataları güzel mesajlara çevir
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Response her zaman JSON olacak
            context.Response.ContentType = "application/json";
            
            // Default values
            string message = "Bir hata oluştu";
            string? details = exception.Message;
            int statusCode = 500;

            // Exception türüne göre farklı status code'lar
            switch (exception)
            {
                case UnauthorizedAccessException:
                    // JWT token geçersiz, yetkisiz erişim
                    statusCode = 401;
                    message = "Giriş yapmanız gerekiyor";
                    details = null;
                    break;
                
                case KeyNotFoundException:
                    // User/Room bulunamadı
                    statusCode = 404;
                    message = "Aradığınız kaynak bulunamadı";
                    details = null;
                    break;
                
                case ArgumentException:
                    // Geçersiz parametre (email format hatası vs)
                    statusCode = 400;
                    message = "Geçersiz veri gönderildi";
                    details = exception.Message;
                    break;
                
                case InvalidOperationException:
                    // Business logic hatası (zaten grup üyesi vs)
                    statusCode = 400;
                    message = "Bu işlem yapılamaz";
                    details = exception.Message;
                    break;
                
                default:
                    // Database hatası, beklenmeyen hatalar
                    statusCode = 500;
                    message = "Sunucu hatası, lütfen daha sonra tekrar deneyin";
                    details = null;
                    break;
            }

            context.Response.StatusCode = statusCode;

            // Response object - always include details field for consistency
            var response = new { message, details };

            // JSON'a çevir ve gönder
            var jsonResponse = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
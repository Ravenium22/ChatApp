using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        // İzin verilen dosya türleri
        private readonly string[] _allowedImageTypes = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly string[] _allowedDocumentTypes = { ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".pptx" };
        private readonly string[] _allowedVideoTypes = { ".mp4", ".avi", ".mov", ".wmv" };
        private readonly string[] _allowedAudioTypes = { ".mp3", ".wav", ".ogg", ".m4a" };

        // Max dosya boyutları (bytes)
        private const long MaxImageSize = 10 * 1024 * 1024; // 10MB
        private const long MaxDocumentSize = 50 * 1024 * 1024; // 50MB
        private const long MaxVideoSize = 200 * 1024 * 1024; // 200MB
        private const long MaxAudioSize = 20 * 1024 * 1024; // 20MB

        public FileController(ApplicationDbContext context, JwtService jwtService, 
            IWebHostEnvironment environment, IConfiguration configuration)
        {
            _context = context;
            _jwtService = jwtService;
            _environment = environment;
            _configuration = configuration;
        }

        // POST: api/file/upload
        [HttpPost("upload")]
        public async Task<ActionResult<FileUploadResponseDto>> UploadFile(IFormFile file)
        {
            var userId = _jwtService.GetUserIdFromToken(HttpContext.User);
            if (!userId.HasValue)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("Dosya seçilmedi");

            // Dosya uzantısını kontrol et
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileType = GetFileType(extension);
            
            if (fileType == FileType.Other)
                return BadRequest("Desteklenmeyen dosya türü");

            // Dosya boyutunu kontrol et
            if (!IsFileSizeValid(file.Length, fileType))
                return BadRequest($"Dosya boyutu çok büyük. Max: {GetMaxSizeForType(fileType) / (1024 * 1024)}MB");

            try
            {
                // Upload klasörünü oluştur
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Unique dosya adı oluştur
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Dosyayı kaydet
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Thumbnail oluştur (sadece resimler için)
                string? thumbnailPath = null;
                if (fileType == FileType.Image)
                {
                    thumbnailPath = await CreateThumbnail(filePath, uniqueFileName, file.ContentType);
                }

                // Database'e kaydet
                var fileAttachment = new FileAttachment
                {
                    FileName = uniqueFileName,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    FilePath = Path.Combine("uploads", uniqueFileName),
                    ThumbnailPath = thumbnailPath,
                    UploadedById = userId.Value,
                    FileType = fileType,
                    UploadedAt = DateTime.UtcNow
                };

                _context.FileAttachments.Add(fileAttachment);
                await _context.SaveChangesAsync();

                // Response DTO
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = new FileUploadResponseDto
                {
                    Id = fileAttachment.Id,
                    FileName = fileAttachment.FileName,
                    OriginalFileName = fileAttachment.OriginalFileName,
                    ContentType = fileAttachment.ContentType,
                    FileSize = fileAttachment.FileSize,
                    FilePath = fileAttachment.FilePath,
                    ThumbnailPath = fileAttachment.ThumbnailPath,
                    UploadedAt = fileAttachment.UploadedAt,
                    FileType = fileAttachment.FileType,
                    FileUrl = $"{baseUrl}/{fileAttachment.FilePath}",
                    ThumbnailUrl = !string.IsNullOrEmpty(fileAttachment.ThumbnailPath) 
                        ? $"{baseUrl}/{fileAttachment.ThumbnailPath}" 
                        : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Dosya yüklenirken hata oluştu", details = ex.Message });
            }
        }

        // GET: api/file/{id} - dosya bilgisi al
        [HttpGet("{id}")]
        public async Task<ActionResult<FileUploadResponseDto>> GetFile(int id)
        {
            var fileAttachment = await _context.FileAttachments.FindAsync(id);
            
            if (fileAttachment == null)
                return NotFound("Dosya bulunamadı");

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var response = new FileUploadResponseDto
            {
                Id = fileAttachment.Id,
                FileName = fileAttachment.FileName,
                OriginalFileName = fileAttachment.OriginalFileName,
                ContentType = fileAttachment.ContentType,
                FileSize = fileAttachment.FileSize,
                FilePath = fileAttachment.FilePath,
                ThumbnailPath = fileAttachment.ThumbnailPath,
                UploadedAt = fileAttachment.UploadedAt,
                FileType = fileAttachment.FileType,
                FileUrl = $"{baseUrl}/{fileAttachment.FilePath}",
                ThumbnailUrl = !string.IsNullOrEmpty(fileAttachment.ThumbnailPath) 
                    ? $"{baseUrl}/{fileAttachment.ThumbnailPath}" 
                    : null
            };

            return Ok(response);
        }

        // Helper methods
        private FileType GetFileType(string extension)
        {
            if (_allowedImageTypes.Contains(extension))
                return FileType.Image;
            else if (_allowedDocumentTypes.Contains(extension))
                return FileType.Document;
            else if (_allowedVideoTypes.Contains(extension))
                return FileType.Video;
            else if (_allowedAudioTypes.Contains(extension))
                return FileType.Audio;
            else
                return FileType.Other;
        }

        private bool IsFileSizeValid(long fileSize, FileType fileType)
        {
            return fileSize <= GetMaxSizeForType(fileType);
        }

        private long GetMaxSizeForType(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => MaxImageSize,
                FileType.Document => MaxDocumentSize,
                FileType.Video => MaxVideoSize,
                FileType.Audio => MaxAudioSize,
                _ => MaxDocumentSize
            };
        }

        private async Task<string?> CreateThumbnail(string originalPath, string fileName, string contentType)
        {
            // Sadece resim dosyaları için thumbnail oluştur
            var supportedFormats = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!supportedFormats.Contains(contentType.ToLower()))
                return null;

            try
            {
                var thumbnailFolder = Path.Combine(_environment.WebRootPath, "uploads", "thumbnails");
                if (!Directory.Exists(thumbnailFolder))
                    Directory.CreateDirectory(thumbnailFolder);

                var thumbnailFileName = $"thumb_{Path.GetFileNameWithoutExtension(fileName)}.jpg";
                var thumbnailPath = Path.Combine(thumbnailFolder, thumbnailFileName);

                using var image = await Image.LoadAsync(originalPath);
                
                // Thumbnail oluştur
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(200, 200),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));
                
                // JPEG encoder ayarları
                var encoder = new JpegEncoder()
                {
                    Quality = 80
                };
                
                await image.SaveAsync(thumbnailPath, encoder);
                
                return Path.Combine("uploads", "thumbnails", thumbnailFileName);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Thumbnail creation failed for {fileName}: {ex.Message}");
                return null;
            }
        }
    }
}
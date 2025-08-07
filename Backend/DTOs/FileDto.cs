using Backend.Models;

namespace Backend.DTOs
{
    public class FileUploadResponseDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public DateTime UploadedAt { get; set; }
        public FileType FileType { get; set; }
        
        // File URL for frontend
        public string FileUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
    }
    
    public class SendFileMessageDto
    {
        public string? Content { get; set; } = string.Empty; // Optional caption
        public int FileAttachmentId { get; set; } // Previously uploaded file
        public int? RoomId { get; set; }
        public int? ReceiverId { get; set; }
        public MessageType Type { get; set; } = MessageType.File;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class FileAttachment
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;
        
        public long FileSize { get; set; }
        
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        public string? ThumbnailPath { get; set; }
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation Properties
        public int UploadedById { get; set; }
        public User UploadedBy { get; set; } = null!;
        
        // Message relationship (bir dosya birden fazla mesajda kullanılabilir)
        public List<Message> Messages { get; set; } = new List<Message>();
        
        public FileType FileType { get; set; }
    }
    
    public enum FileType
    {
        Document = 0,
        Image = 1,
        Video = 2,
        Audio = 3,
        Other = 4
    }
}
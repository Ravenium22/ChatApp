using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs
{
    public class CreateRoomDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        }
    
    public class RoomResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        
        public UserResponseDto CreatedBy { get; set; } = null!;
        public int MemberCount { get; set; }
        public List<RoomMemberDto> Members { get; set; } = new List<RoomMemberDto>();
        public LastMessageDto? LastMessage { get; set; }
    }
    
    public class RoomMemberDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public RoomRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsOnline { get; set; }
    }
    
    public class LastMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string SenderName { get; set; } = string.Empty;
    }
    
    public class JoinRoomDto
    {
        [Required]
        public int UserId { get; set; }
    }
    
    public class LeaveRoomDto
    {
        [Required]
        public int UserId { get; set; }
    }
    
    public class UserRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public RoomRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public UserResponseDto CreatedBy { get; set; } = null!;
        public LastMessageDto? LastMessage { get; set; }
    }
}
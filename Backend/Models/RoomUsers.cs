namespace Backend.Models
{
    public class RoomUser
    {
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public DateTime? LeftAt { get; set; } = null;
        
        public RoomRole Role { get; set; } = RoomRole.Member;

        public Room Room { get; set; } = null!;
        public User User { get; set; } = null!;
    }
    
    public enum RoomRole
    {
        Member = 0,
        Admin = 1,
        Owner = 2
    }
}
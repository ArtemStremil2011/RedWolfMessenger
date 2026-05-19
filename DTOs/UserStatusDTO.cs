namespace Messenger.DTOs
{
    public class UserStatusDTO
    {
        public Guid UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}
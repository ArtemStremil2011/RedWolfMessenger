using Messenger.Models.BaseModels;

namespace Messenger.Models.ChatModels
{
    public interface IChat
    {
        public Guid Id { get; set; }
        public string ChatName { get; set; }
        public ICollection<User> Users { get; set; }
        public ICollection<Message> MessagesHistory { get; set; }
        public int MaxUsers { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public User? CreatedBy { get; set; }

        public void AddUser(User user);
        public void AddUser(User user, User? addedBy);
        public void RemoveUser(User user);
        public void RemoveUser(User user, User? removedBy);
        public bool IsUserInChat(User user);
        public bool CanUserManageUsers(User user);
    }
}
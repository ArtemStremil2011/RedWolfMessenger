// Group.cs - имплементирует IChat
using System.ComponentModel.DataAnnotations;
using Messenger.Models.BaseModels;

namespace Messenger.Models.ChatModels
{
    public class Group : IChat
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ChatName { get; set; } = string.Empty; // ← GroupName → ChatName

        public string? Description { get; set; }

        public string? AvatarPath { get; set; }

        public int MaxUsers { get; set; } = 100;

        public Guid CreatedById { get; set; }
        public virtual User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }

        // IChat требует ICollection<User> Users
        public virtual ICollection<User> Users { get; set; } = new List<User>();

        // IChat требует ICollection<Message> MessagesHistory
        public virtual ICollection<Message> MessagesHistory { get; set; } = new List<Message>();

        public bool IsPrivate { get; set; } = true;

        public Group()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        // Реализация методов IChat
        public void AddUser(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (Users.Count >= MaxUsers) throw new InvalidOperationException($"Группа не может содержать более {MaxUsers} участников");
            if (!IsUserInChat(user))
            {
                Users.Add(user);
                LastActivityAt = DateTime.UtcNow;
            }
        }

        public void AddUser(User user, User? addedBy)
        {
            AddUser(user);
        }

        public void RemoveUser(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            Users.Remove(user);
            LastActivityAt = DateTime.UtcNow;
        }

        public void RemoveUser(User user, User? removedBy)
        {
            RemoveUser(user);
        }

        public bool IsUserInChat(User user)
        {
            return user != null && Users.Any(u => u.Id == user.Id);
        }

        public bool CanUserManageUsers(User user)
        {
            // Для групп: создатель и админы могут управлять
            if (!IsUserInChat(user)) return false;
            return CreatedBy?.Id == user.Id; // Пока только создатель, потом добавим роли
        }

        public User? GetOtherUser(User currentUser)
        {
            // Для групп нет понятия "другой пользователь"
            return null;
        }
    }
}
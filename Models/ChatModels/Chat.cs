using Messenger.Models.BaseModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Messenger.Models.ChatModels
{
    public class Chat : IChat
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ChatName { get; set; } = string.Empty;

        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Message> MessagesHistory { get; set; } = new List<Message>();

        public int MaxUsers { get; set; } = 2;
        public bool IsPrivate { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? LastActivityAt { get; set; }

        public Guid? CreatedById { get; set; }

        [ForeignKey(nameof(CreatedById))]
        public virtual User? CreatedBy { get; set; }

        public Chat()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public void AddUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (Users.Count >= MaxUsers)
                throw new InvalidOperationException($"Личный чат не может содержать более {MaxUsers} пользователей");

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
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (Users.Count <= 1 && IsUserInChat(user))
                throw new InvalidOperationException("В чате должен быть хотя бы один пользователь");

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
            return IsUserInChat(user);
        }

        public User? GetOtherUser(User currentUser)
        {
            return Users.FirstOrDefault(u => u.Id != currentUser?.Id);
        }
    }
}
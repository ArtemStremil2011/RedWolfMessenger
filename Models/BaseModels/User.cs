using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Messenger.Models.ChatModels;

namespace Messenger.Models.BaseModels
{
    public enum UserRole
    {
        User = 0,
        Admin = 1,
        SuperAdmin = 2
    }

    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string? AvatarPath { get; set; }

        public DateTime RegisterDate { get; set; }

        public bool IsPhoneNumberConfirmed { get; set; } = false;
        public string? PhoneVerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }

        public UserRole Role { get; set; } = UserRole.User;

        // ============ КРИПТОГРАФИЯ ============
        [StringLength(500)]
        public string? PublicKey { get; set; }

        [StringLength(1000)]
        public string? EncryptedPrivateKey { get; set; }

        [StringLength(100)]
        public string? KeySalt { get; set; }

        [StringLength(100)]
        public string? KeyIv { get; set; }

        public virtual ICollection<Message>? Messages { get; set; }
        public virtual ICollection<Chat>? Chats { get; set; }

        public User()
        {
            Id = Guid.NewGuid();
            RegisterDate = DateTime.UtcNow;
        }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Messenger.Models.BaseModels;

namespace Messenger.Models.ChatModels
{
    [Table("ChatSessionKeys")]
    public class ChatSessionKey
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat? Chat { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [Required]
        [StringLength(1000)]
        public string EncryptedSessionKey { get; set; } = string.Empty;  // Зашифровано публичным ключом пользователя

        public DateTime CreatedAt { get; set; }

        public ChatSessionKey()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Messenger.Models.ChatModels;

namespace Messenger.Models.BaseModels
{
    [Table("ModeratedMessages")]
    public class ModeratedMessage
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid MessageId { get; set; }

        [Required]
        [StringLength(5000)]
        public string PlainText { get; set; } = string.Empty;

        [Required]
        public Guid ChatId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        // Навигационные свойства (опционально)
        [ForeignKey(nameof(MessageId))]
        public virtual Message? Message { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat? Chat { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        public ModeratedMessage()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
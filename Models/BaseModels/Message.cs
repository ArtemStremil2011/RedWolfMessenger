using Messenger.Models.ChatModels;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models.BaseModels
{
    public class Message
    {
        [Key]
        public Guid MessageId { get; set; }

        [Required]
        [StringLength(5000)]
        public string MessageText { get; set; } = string.Empty;

        [Required]
        public DateTime MessageCreateDate { get; set; }

        public DateTime MessageLastUpdateDate { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? MessageCreator { get; set; }

        public bool IsDeleted { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat? Chat { get; set; }

        public Message()
        {
            MessageId = Guid.NewGuid();
            MessageCreateDate = DateTime.UtcNow;
            MessageLastUpdateDate = DateTime.UtcNow;
        }
    }
}
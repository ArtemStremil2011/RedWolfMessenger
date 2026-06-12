using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Messenger.Models.ChatModels;

namespace Messenger.Models.BaseModels
{
    public class Message
    {
        [Key]
        public Guid MessageId { get; set; }

        // ============ СТАРОЕ ПОЛЕ (ВРЕМЕННО) ============
        // MessageText пока оставляем, потом удалим
        [StringLength(5000)]
        public string? MessageText { get; set; } = string.Empty;

        // ============ НОВЫЕ ПОЛЯ ДЛЯ ШИФРОВАНИЯ ============
        [StringLength(5000)]
        public string? EncryptedData { get; set; }  // Зашифрованное сообщение (base64)

        [StringLength(50)]
        public string? Iv { get; set; }  // Вектор инициализации (base64)

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

        public bool IsSystemMessage { get; set; } = false;

        public bool IsRead { get; set; } = false;

        public Message()
        {
            MessageId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            MessageCreateDate = now;
            MessageLastUpdateDate = now;
        }
    }
}
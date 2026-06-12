using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Messenger.Models.BaseModels;

namespace Messenger.Models.ChatModels
{
    [Table("FileMessages")]
    public class FileMessage
    {
        [Key]
        public Guid MessageId { get; set; }

        [StringLength(5000)]
        public string? MessageText { get; set; }

        // ============ НОВЫЕ ПОЛЯ ДЛЯ ШИФРОВАНИЯ ============
        [StringLength(5000)]
        public string? EncryptedData { get; set; }

        [StringLength(50)]
        public string? Iv { get; set; }

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

        // Поля для файла
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public FileMessage()
        {
            MessageId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            MessageCreateDate = now;
            MessageLastUpdateDate = now;
        }
    }
}
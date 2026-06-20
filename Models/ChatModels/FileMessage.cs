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

        // ============ ШИФРОВАНИЕ ============
        [StringLength(5000)]
        public string? EncryptedData { get; set; }

        [StringLength(50)]
        public string? Iv { get; set; }

        // ============ ОСНОВНЫЕ ПОЛЯ ============
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

        // ============ ПОЛЯ ДЛЯ ФАЙЛОВ ============
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

        // ============ НОВЫЕ ПОЛЯ ДЛЯ ГОЛОСОВЫХ ============
        public int? Duration { get; set; } // Длительность в секундах (только для голосовых)

        [StringLength(20)]
        public string MessageType { get; set; } = "file"; // "file" | "voice"

        public FileMessage()
        {
            MessageId = Guid.NewGuid();
            MessageCreateDate = DateTime.UtcNow;
            MessageLastUpdateDate = DateTime.UtcNow;
        }
    }
}
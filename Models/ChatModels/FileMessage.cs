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

        public bool IsSystemMessage { get; set; } = false;

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
            MessageCreateDate = DateTime.UtcNow;
            MessageLastUpdateDate = DateTime.UtcNow;
        }
    }
}
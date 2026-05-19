using Messenger.Models.BaseModels;
using System.ComponentModel.DataAnnotations;

namespace Messenger.Models.ChatModels
{
    public class FileMessage : Message
    {
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

        // НЕ ДОБАВЛЯЙ User - он уже есть в Message как MessageCreator
    }
}
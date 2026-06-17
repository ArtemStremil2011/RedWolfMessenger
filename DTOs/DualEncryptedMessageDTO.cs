using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public class DualEncryptedMessageCreateDTO
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        [Required]
        public string EncryptedForUsers { get; set; } = string.Empty;

        [Required]
        public string IvForUsers { get; set; } = string.Empty;

        // Делаем НЕ обязательными - убираем [Required]
        public string? EncryptedForServer { get; set; }
        
        public string? IvForServer { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public class DualEncryptedMessageCreateDTO
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        // Для пользователей чата (зашифровано сессионным ключом чата)
        [Required]
        public string EncryptedForUsers { get; set; } = string.Empty;

        [Required]
        public string IvForUsers { get; set; } = string.Empty;

        // Для сервера (зашифровано публичным ключом сервера)
        [Required]
        public string EncryptedForServer { get; set; } = string.Empty;

        [Required]
        public string IvForServer { get; set; } = string.Empty;
    }
}
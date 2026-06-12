using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public class FileMessageCreateDTO
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        public Guid ChatId { get; set; }

        public string? Caption { get; set; }
    }

    public record FileMessageResponseDTO(
        Guid MessageId,
        string? Caption,
        DateTime MessageCreateDate,
        DateTime? MessageLastUpdateDate,
        Guid UserId,
        Guid ChatId,
        UserResponseDTO? User,
        string FileName,
        string FilePath,
        long FileSize,
        string ContentType
    );
}
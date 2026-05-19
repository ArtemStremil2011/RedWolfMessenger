using System.ComponentModel.DataAnnotations;
using Messenger.DTOs;

namespace Messenger.Models.BaseModels
{
    // DTO для создания файлового сообщения
    public class FileMessageCreateDTO
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        public Guid ChatId { get; set; }

        public string? Caption { get; set; }
    }

    // DTO для ответа
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

    // DTO для скачивания файла
    public record FileDownloadDTO(
        Guid FileId,
        string FileName,
        string ContentType,
        byte[] Content
    );
}
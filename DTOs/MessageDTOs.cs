using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public record MessageResponseDTO(
        Guid MessageId,
        string? MessageText,
        DateTime MessageCreateDate,
        DateTime? MessageLastUpdateDate,
        Guid UserId,
        Guid ChatId,
        UserResponseDTO? MessageCreator,
        bool IsDeleted,
        bool IsSystemMessage,
        string? FileName = null,
        long? FileSize = null,
        string? ContentType = null
    );

    public record MessageCreateDTO(
        [Required]
        [StringLength(5000)]
        string MessageText,

        [Required]
        Guid UserId,

        [Required]
        Guid ChatId
    );

    public record MessageUpdateDTO(
        [Required]
        Guid MessageId,

        [Required]
        [StringLength(5000)]
        string MessageText
    );
}
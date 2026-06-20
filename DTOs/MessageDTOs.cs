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
        bool IsRead,
        string? EncryptedData = null,
        string? Iv = null,
        string? FileName = null,
        long? FileSize = null,
        string? ContentType = null,
        int? Duration = null
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

        [StringLength(5000)]
        string? MessageText = null,
        
        [StringLength(5000)]
        string? EncryptedData = null,
        
        [StringLength(50)]
        string? Iv = null
    );

    public record EncryptedMessageCreateDTO(
        [Required]
        Guid UserId,

        [Required]
        Guid ChatId,

        [Required]
        string EncryptedData,

        [Required]
        string Iv
    );
}
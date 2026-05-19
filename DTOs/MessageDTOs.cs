using Messenger.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    // DTOs/MessageResponseDTO.cs - верни обратно без IsSystemMessage
    // DTOs/MessageResponseDTO.cs
    public record MessageResponseDTO(
        Guid MessageId,
        string? MessageText,
        DateTime MessageCreateDate,
        DateTime? MessageLastUpdateDate,
        Guid UserId,
        Guid ChatId,
        UserResponseDTO? MessageCreator,
        bool IsDeleted,
        bool IsSystemMessage  // ← убедись, что это поле есть
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
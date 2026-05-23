using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public record ChatResponseDTO(
        Guid Id,
        string ChatName,
        ICollection<UserResponseDTO> Users,
        UserResponseDTO? OtherUser,
        int MaxUsers,
        DateTime CreatedAt,
        DateTime? LastActivityAt,
        string? AvatarPath  // ← ЭТОТ ПАРАМЕТР ДОЛЖЕН БЫТЬ
    );

    public record CreateChatDTO(
        [Required]
        [MinLength(2)]
        List<Guid> MemberIds,
        int? MaxUsers,
        string? ChatName
    );

    public record UpdateChatNameDTO(
        [Required]
        [StringLength(100)]
        string ChatName
    );

    public record AddUserToChatDTO(
        [Required] Guid ChatId,
        [Required] Guid UserId
    );
}
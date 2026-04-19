using Messenger.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public abstract record ChatBaseDTO(
        Guid Id,
        string ChatName,
        int UsersCount,
        int MaxUsers,
        bool IsPrivate,
        DateTime CreatedAt,
        DateTime? LastActivityAt,
        UserResponseDTO? CreatedBy
    );

    public record ChatResponseDTO(
        Guid Id,
        string ChatName,
        ICollection<UserResponseDTO> Users,
        UserResponseDTO? OtherUser,
        DateTime CreatedAt,
        DateTime? LastActivityAt
    ) : ChatBaseDTO(Id, ChatName, Users.Count, 2, true, CreatedAt, LastActivityAt, null);

    public record CreateChatDTO(
        [Required] Guid User1Id,
        [Required] Guid User2Id,
        string? ChatName
    );

    public record CreateChatByNameDTO(
        [Required]
        [StringLength(50, MinimumLength = 2)]
        string Username,

        string? ChatName
    );
}
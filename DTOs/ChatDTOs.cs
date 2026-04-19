using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    // Базовый DTO
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

    // DTO для ответа
    public record ChatResponseDTO(
        Guid Id,
        string ChatName,
        ICollection<UserResponseDTO> Users,
        UserResponseDTO? OtherUser,
        int MaxUsers,
        DateTime CreatedAt,
        DateTime? LastActivityAt
    ) : ChatBaseDTO(Id, ChatName, Users.Count, MaxUsers, true, CreatedAt, LastActivityAt, null);

    // DTO для создания чата (универсальный)
    public record CreateChatDTO(
        [Required]
        [MinLength(2, ErrorMessage = "Минимум 2 участника")]
        List<Guid> MemberIds,

        [Range(2, 500, ErrorMessage = "MaxUsers от 2 до 500")]
        int? MaxUsers,

        [StringLength(100)]
        string? ChatName
    );
}
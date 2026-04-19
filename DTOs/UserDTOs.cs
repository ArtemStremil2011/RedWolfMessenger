using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public record UserResponseDTO(
        Guid Id,
        string Name,
        string? AvatarPath,
        DateTime RegisterDate
    );

    public record UserCreateDTO(
        [Required]
        [StringLength(50, MinimumLength = 2)]
        string Name,

        [Required]
        [StringLength(100, MinimumLength = 6)]
        string Password
    );

    public record UserUpdateDTO(
        [StringLength(50, MinimumLength = 2)]
        string? Name,

        string? AvatarPath,

        [StringLength(100, MinimumLength = 6)]
        string? NewPassword
    );

    public record UserLoginDTO(
        [Required]
        string Login,

        [Required]
        string Password
    );

    public record UserSearchDTO(
        Guid Id,
        string Name,
        string? AvatarPath
    );

    public class VerifyCodeDTO
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public record UserRegisterDTO(
        [Required]
        [StringLength(20, MinimumLength = 10)]
        string PhoneNumber,

        [Required]
        [StringLength(50, MinimumLength = 2)]
        string Name,

        [Required]
        [StringLength(100, MinimumLength = 6)]
        string Password,

        string? AvatarPath  // ← добавить
    );

    public record VerifyPhoneDTO(
        [Required]
        string PhoneNumber,

        [Required]
        string Code
    );

    public record ResendVerificationCodeDTO(
        [Required]
        string PhoneNumber
    );
}
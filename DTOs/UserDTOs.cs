using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public record UserResponseDTO(
        Guid Id,
        string Name,
        string? AvatarPath,
        DateTime RegisterDate,
        string? PublicKey = null
    );

    public record UserRegisterDTO(
        [Required]
        [StringLength(20, MinimumLength = 10)]
        string PhoneNumber,

        [Required]
        [StringLength(50, MinimumLength = 2)]
        string Name,

        [Required]
        [StringLength(100, MinimumLength = 6)]
        string Password
    );

    public record VerifyPhoneDTO(
        [Required]
        string PhoneNumber,

        [Required]
        string Code
    );

    public record UserLoginDTO(
        [Required]
        string Login,

        [Required]
        string Password
    );

    public record UserUpdateDTO(
        [StringLength(50, MinimumLength = 2)]
        string? Name,

        [StringLength(100, MinimumLength = 6)]
        string? NewPassword
    );

    public record SavePublicKeyDTO(
        [Required]
        string PublicKey
    );

    public record SaveEncryptedPrivateKeyDTO(
        [Required]
        string Data,

        [Required]
        string Salt,

        [Required]
        string Iv
    );
}
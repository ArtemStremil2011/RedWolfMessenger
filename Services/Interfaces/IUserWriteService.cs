using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IUserWriteService
    {
        Task<(bool success, string token, Guid userId)> RegisterAsync(UserRegisterDTO registerDto);
        Task<(bool success, string token, Guid userId)> LoginAsync(UserLoginDTO loginDto);
        Task<bool> UpdateProfileAsync(Guid userId, UserUpdateDTO updateDto);
        Task<bool> UploadAvatarAsync(Guid userId, IFormFile file);
        Task<bool> DeleteAvatarAsync(Guid userId);
        Task<string?> RequestVerificationCodeAsync(UserRegisterDTO registerDto);
        Task<(bool success, string token, Guid userId)> VerifyAndRegisterAsync(string phoneNumber, string code);
        Task<bool> DeleteUserAsync(Guid userId, Guid currentUserId);
    }
}
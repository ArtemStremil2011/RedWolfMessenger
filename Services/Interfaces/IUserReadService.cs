using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IUserReadService
    {
        Task<UserResponseDTO?> GetProfileAsync(Guid userId);
        Task<UserResponseDTO?> GetUserByIdAsync(Guid userId, Guid currentUserId);
        Task<List<UserResponseDTO>> GetAllUsersAsync();
        Task<UserResponseDTO?> GetUserByNameAsync(string name);
        Task<bool> UserExistsAsync(Guid userId);
    }
}
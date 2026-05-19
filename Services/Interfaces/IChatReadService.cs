using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IChatReadService
    {
        Task<List<ChatResponseDTO>> GetAllChatsAsync(Guid currentUserId);
        Task<ChatResponseDTO?> GetChatAsync(Guid chatId, Guid currentUserId);
        Task<List<ChatResponseDTO>> GetUserChatsAsync(Guid userId, Guid currentUserId);
        Task<List<MessageResponseDTO>> GetChatMessagesAsync(Guid chatId, Guid currentUserId, int page = 1, int pageSize = 50);
        Task<int> GetTotalMessagesCountAsync(Guid chatId);
        Task<bool> UserInChatAsync(Guid chatId, Guid userId);
        Task<UserStatusDTO> GetUserStatusAsync(Guid userId);
        Task<UserResponseDTO?> GetUserProfileForChatAsync(Guid userId, Guid currentUserId);
    }
}
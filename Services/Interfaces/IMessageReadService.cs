using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IMessageReadService
    {
        Task<MessageResponseDTO?> GetMessageByIdAsync(Guid messageId, Guid currentUserId);
        Task<List<MessageResponseDTO>> GetAllMessagesAsync(Guid currentUserId);
        Task<List<MessageResponseDTO>> GetChatMessagesAsync(Guid chatId, Guid currentUserId, int page = 1, int pageSize = 50);
        Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid userId);
        Task<List<MessageResponseDTO>> GetDeletedMessagesAsync(Guid chatId, Guid userId);
    }
}
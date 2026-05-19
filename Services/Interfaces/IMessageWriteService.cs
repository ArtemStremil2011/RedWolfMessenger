using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IMessageWriteService
    {
        Task<MessageResponseDTO?> CreateMessageAsync(Guid userId, Guid chatId, string text);
        Task<MessageResponseDTO?> UpdateMessageAsync(Guid messageId, string newText, Guid currentUserId);
        Task<bool> DeleteMessageAsync(Guid messageId, Guid currentUserId);
        Task<bool> PermanentDeleteMessageAsync(Guid messageId, Guid currentUserId);
        Task<bool> RestoreMessageAsync(Guid messageId, Guid currentUserId);
    }
}
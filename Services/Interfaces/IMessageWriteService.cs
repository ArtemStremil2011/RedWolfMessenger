using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IMessageWriteService
    {
        // Обычные сообщения
        Task<MessageResponseDTO?> CreateMessageAsync(Guid userId, Guid chatId, string text);
        Task<MessageResponseDTO?> UpdateMessageAsync(Guid messageId, string newText, Guid currentUserId);
        
        // Зашифрованные сообщения
        Task<MessageResponseDTO?> CreateEncryptedMessageAsync(Guid userId, Guid chatId, string encryptedData, string iv);
        Task<MessageResponseDTO?> UpdateEncryptedMessageAsync(Guid messageId, string encryptedData, string iv, Guid currentUserId);
        
        // Управление сообщениями
        Task<bool> DeleteMessageAsync(Guid messageId, Guid currentUserId);
        Task<bool> PermanentDeleteMessageAsync(Guid messageId, Guid currentUserId);
        Task<bool> RestoreMessageAsync(Guid messageId, Guid currentUserId);
        Task MarkMessagesAsReadAsync(Guid chatId, Guid userId);
    }
}
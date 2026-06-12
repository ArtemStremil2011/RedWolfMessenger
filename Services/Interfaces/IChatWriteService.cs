using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IChatWriteService
    {
        Task<ChatResponseDTO?> CreateChatAsync(CreateChatDTO dto, Guid currentUserId);
        Task<bool> UpdateChatNameAsync(Guid chatId, string newName, Guid currentUserId);
        Task<bool> AddUserToChatAsync(Guid chatId, Guid userIdToAdd, Guid currentUserId);
        Task<bool> RemoveUserFromChatAsync(Guid chatId, Guid userIdToRemove, Guid currentUserId);
        Task<bool> DeleteChatAsync(Guid chatId, Guid currentUserId);
        Task<bool> LeaveGroupAsync(Guid chatId, Guid currentUserId);
        Task<string?> UploadGroupAvatarAsync(Guid chatId, IFormFile file, Guid currentUserId);
        Task<bool> DeleteGroupAvatarAsync(Guid chatId, Guid currentUserId);
        Task<bool> SaveSessionKeysAsync(Guid chatId, Dictionary<Guid, string> encryptedKeys, Guid currentUserId);
    }
}
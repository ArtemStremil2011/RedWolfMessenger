using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IFileReadService
    {
        Task<FileMessageResponseDTO?> GetFileMessageAsync(Guid messageId, Guid currentUserId);
        Task<List<FileMessageResponseDTO>> GetChatFilesAsync(Guid chatId, Guid currentUserId);
        Task<bool> UserHasAccessToFileAsync(Guid messageId, Guid currentUserId);
    }
}
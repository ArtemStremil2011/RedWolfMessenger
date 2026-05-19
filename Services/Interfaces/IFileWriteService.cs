using Messenger.Models.BaseModels;

namespace Messenger.Services.Interfaces
{
    public interface IFileWriteService
    {
        Task<FileMessageResponseDTO?> UploadFileAsync(Guid chatId, IFormFile file, string? caption, Guid currentUserId);
        Task<bool> DeleteFileAsync(Guid messageId, Guid currentUserId);
        Task<bool> DeletePhysicalFileAsync(string filePath);
    }
}
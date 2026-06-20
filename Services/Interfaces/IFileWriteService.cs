using Messenger.DTOs;

namespace Messenger.Services.Interfaces
{
    public interface IFileWriteService
    {
        Task<FileMessageResponseDTO?> UploadFileAsync(
            Guid chatId,
            IFormFile file,
            string? caption,
            Guid currentUserId,
            bool isVoice = false,
            int? duration = null);

        Task<bool> DeleteFileAsync(Guid messageId, Guid currentUserId);
        Task<bool> DeletePhysicalFileAsync(string filePath);
    }
}
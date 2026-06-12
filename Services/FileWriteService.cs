using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.ChatModels;

namespace Messenger.Services
{
    public class FileWriteService : IFileWriteService
    {
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileWriteService> _logger;

        public FileWriteService(
            AppDBContext context,
            IWebHostEnvironment environment,
            ILogger<FileWriteService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<FileMessageResponseDTO?> UploadFileAsync(Guid chatId, IFormFile file, string? caption, Guid currentUserId)
        {
            try
            {
                if (file.Length > 50 * 1024 * 1024)
                {
                    _logger.LogWarning("File too large: {Size} bytes", file.Length);
                    return null;
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".json", ".xml", ".csv" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension: {Extension}", extension);
                    return null;
                }

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", currentUserId, chatId);
                    return null;
                }

                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var fileName = $"{DateTime.Now.Ticks}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);
                var relativePath = $"/uploads/{fileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var fileMessage = new FileMessage
                {
                    MessageId = Guid.NewGuid(),
                    MessageText = caption ?? $"📎 {file.FileName}",
                    FileName = file.FileName,
                    FilePath = relativePath,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    UserId = currentUserId,
                    ChatId = chatId,
                    MessageCreateDate = DateTime.UtcNow,
                    MessageLastUpdateDate = DateTime.UtcNow,
                    IsDeleted = false,
                    IsSystemMessage = false,
                    IsRead = false
                };

                await _context.Set<FileMessage>().AddAsync(fileMessage);
                await _context.SaveChangesAsync();

                await _context.Entry(fileMessage).Reference(f => f.MessageCreator).LoadAsync();

                _logger.LogInformation("File uploaded: {FileName} to chat {ChatId}", file.FileName, chatId);

                return new FileMessageResponseDTO(
                    fileMessage.MessageId,
                    fileMessage.MessageText,
                    fileMessage.MessageCreateDate,
                    fileMessage.MessageLastUpdateDate,
                    fileMessage.UserId,
                    fileMessage.ChatId,
                    fileMessage.MessageCreator != null ? new UserResponseDTO(
                        fileMessage.MessageCreator.Id,
                        fileMessage.MessageCreator.Name,
                        fileMessage.MessageCreator.AvatarPath,
                        fileMessage.MessageCreator.RegisterDate
                    ) : null,
                    fileMessage.FileName,
                    fileMessage.FilePath,
                    fileMessage.FileSize,
                    fileMessage.ContentType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return null;
            }
        }

        public async Task<bool> DeleteFileAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var fileMessage = await _context.Set<FileMessage>()
                    .FirstOrDefaultAsync(f => f.MessageId == messageId);

                if (fileMessage == null) return false;

                if (fileMessage.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} tried to delete file {MessageId} without permission", currentUserId, messageId);
                    return false;
                }

                await DeletePhysicalFileAsync(fileMessage.FilePath);

                _context.Set<FileMessage>().Remove(fileMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("File {MessageId} deleted", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {MessageId}", messageId);
                return false;
            }
        }

        public async Task<bool> DeletePhysicalFileAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting physical file {FilePath}", filePath);
                return false;
            }
        }
    }
}

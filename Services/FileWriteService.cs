using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.ChatModels;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        public async Task<FileMessageResponseDTO?> UploadFileAsync(
            Guid chatId,
            IFormFile file,
            string? caption,
            Guid currentUserId,
            bool isVoice = false,
            int? duration = null)
        {
            try
            {
                // ===== ПРОВЕРКА РАЗМЕРА =====
                var maxSize = isVoice ? 10 * 1024 * 1024 : 50 * 1024 * 1024; // 10MB для голосовых, 50MB для файлов
                if (file.Length > maxSize)
                {
                    _logger.LogWarning($"File too large: {file.Length} bytes (max: {maxSize})");
                    return null;
                }

                // ===== ПРОВЕРКА РАСШИРЕНИЯ =====
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                // Для голосовых сообщений разрешены только аудио форматы
                if (isVoice)
                {
                    var voiceExtensions = new[] { ".webm", ".mp3", ".ogg", ".wav" };
                    if (!voiceExtensions.Contains(extension))
                    {
                        _logger.LogWarning($"Invalid voice format: {extension}");
                        return null;
                    }
                }
                else
                {
                    var allowedExtensions = new[] {
                        ".jpg", ".jpeg", ".png", ".gif", ".webp",
                        ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx",
                        ".ppt", ".pptx", ".zip", ".rar", ".7z", ".json", ".xml", ".csv"
                    };
                    if (!allowedExtensions.Contains(extension))
                    {
                        _logger.LogWarning($"Invalid file extension: {extension}");
                        return null;
                    }
                }

                // ===== ПРОВЕРКА ЧАТА =====
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning($"User {currentUserId} not in chat {chatId}");
                    return null;
                }

                // ===== СОХРАНЕНИЕ ФАЙЛА =====
                var uploadPath = isVoice
                    ? Path.Combine(_environment.WebRootPath, "voice-messages")
                    : Path.Combine(_environment.WebRootPath, "uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var fileName = $"{DateTime.Now.Ticks}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);
                var relativePath = $"{(isVoice ? "/voice-messages/" : "/uploads/")}{fileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // ===== СОЗДАНИЕ СООБЩЕНИЯ =====
                FileMessage fileMessage;

                if (isVoice)
                {
                    fileMessage = new VoiceMessage
                    {
                        Duration = duration ?? 0,
                        FileName = file.FileName,
                        FilePath = relativePath,
                        FileSize = file.Length,
                        ContentType = file.ContentType ?? "audio/webm",
                        UserId = currentUserId,
                        ChatId = chatId,
                        MessageText = caption ?? "🎤 Voice message",
                        MessageCreateDate = DateTime.UtcNow,
                        MessageLastUpdateDate = DateTime.UtcNow
                    };
                }
                else
                {
                    fileMessage = new FileMessage
                    {
                        FileName = file.FileName,
                        FilePath = relativePath,
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        UserId = currentUserId,
                        ChatId = chatId,
                        MessageText = caption ?? $"📎 {file.FileName}",
                        MessageCreateDate = DateTime.UtcNow,
                        MessageLastUpdateDate = DateTime.UtcNow,
                        MessageType = "file"
                    };
                }

                await _context.Set<FileMessage>().AddAsync(fileMessage);
                await _context.SaveChangesAsync();

                await _context.Entry(fileMessage).Reference(f => f.MessageCreator).LoadAsync();

                _logger.LogInformation($"{(isVoice ? "Voice" : "File")} uploaded: {file.FileName} to chat {chatId}");

                // ===== ВОЗВРАТ DTO =====
                var creator = fileMessage.MessageCreator != null
                    ? new UserResponseDTO(
                        fileMessage.MessageCreator.Id,
                        fileMessage.MessageCreator.Name ?? "",
                        fileMessage.MessageCreator.AvatarPath ?? "",
                        fileMessage.MessageCreator.RegisterDate
                    )
                    : null;

                return new FileMessageResponseDTO(
                    fileMessage.MessageId,
                    fileMessage.MessageText,
                    fileMessage.MessageCreateDate,
                    fileMessage.MessageLastUpdateDate,
                    fileMessage.UserId,
                    fileMessage.ChatId,
                    creator,
                    fileMessage.FileName ?? "",
                    fileMessage.FilePath ?? "",
                    fileMessage.FileSize,
                    fileMessage.ContentType ?? "",
                    fileMessage.MessageType,
                    fileMessage.Duration
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
                    _logger.LogWarning($"User {currentUserId} tried to delete file {messageId} without permission");
                    return false;
                }

                await DeletePhysicalFileAsync(fileMessage.FilePath);

                _context.Set<FileMessage>().Remove(fileMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"File {messageId} deleted");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file {messageId}");
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
                _logger.LogError(ex, $"Error deleting physical file {filePath}");
                return false;
            }
        }
    }
}
using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.ChatModels;

namespace Messenger.Services
{
    public class FileReadService : IFileReadService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<FileReadService> _logger;

        public FileReadService(AppDBContext context, ILogger<FileReadService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FileMessageResponseDTO?> GetFileMessageAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var fileMessage = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .FirstOrDefaultAsync(f => f.MessageId == messageId);

                if (fileMessage == null) return null;

                var hasAccess = await UserHasAccessToFileAsync(messageId, currentUserId);
                if (!hasAccess) return null;

                var creator = fileMessage.MessageCreator != null
                    ? new UserResponseDTO(
                        fileMessage.MessageCreator.Id,
                        fileMessage.MessageCreator.Name ?? "",
                        fileMessage.MessageCreator.AvatarPath ?? "",
                        fileMessage.MessageCreator.RegisterDate,
                        fileMessage.MessageCreator.PublicKey
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
                    fileMessage.ContentType ?? ""
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<List<FileMessageResponseDTO>> GetChatFilesAsync(Guid chatId, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", currentUserId, chatId);
                    return new List<FileMessageResponseDTO>();
                }

                var files = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .Where(f => f.ChatId == chatId && !f.IsDeleted)
                    .OrderBy(f => f.MessageCreateDate)
                    .ToListAsync();

                var result = new List<FileMessageResponseDTO>();
                foreach (var fileMsg in files)
                {
                    var creator = fileMsg.MessageCreator != null
                        ? new UserResponseDTO(
                            fileMsg.MessageCreator.Id,
                            fileMsg.MessageCreator.Name ?? "",
                            fileMsg.MessageCreator.AvatarPath ?? "",
                            fileMsg.MessageCreator.RegisterDate,
                            fileMsg.MessageCreator.PublicKey
                        )
                        : null;

                    result.Add(new FileMessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        creator,
                        fileMsg.FileName ?? "",
                        fileMsg.FilePath ?? "",
                        fileMsg.FileSize,
                        fileMsg.ContentType ?? ""
                    ));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files for chat {ChatId}", chatId);
                return new List<FileMessageResponseDTO>();
            }
        }

        public async Task<bool> UserHasAccessToFileAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var fileMessage = await _context.Set<FileMessage>()
                    .FirstOrDefaultAsync(f => f.MessageId == messageId);

                if (fileMessage == null) return false;

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == fileMessage.ChatId);

                return chat != null && chat.Users.Any(u => u.Id == currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking access for file {MessageId}", messageId);
                return false;
            }
        }
    }
}
using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;

namespace Messenger.Services
{
    public class MessageReadService : IMessageReadService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<MessageReadService> _logger;

        public MessageReadService(AppDBContext context, ILogger<MessageReadService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MessageResponseDTO?> GetMessageByIdAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                // Проверяем в текстовых сообщениях
                var textMessage = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (textMessage != null)
                {
                    var chat = await _context.Chats
                        .Include(c => c.Users)
                        .FirstOrDefaultAsync(c => c.Id == textMessage.ChatId);

                    if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                        return null;

                    return new MessageResponseDTO(
                        textMessage.MessageId,
                        textMessage.MessageText,
                        textMessage.MessageCreateDate,
                        textMessage.MessageLastUpdateDate,
                        textMessage.UserId,
                        textMessage.ChatId,
                        textMessage.MessageCreator != null ? new UserResponseDTO(
                            textMessage.MessageCreator.Id,
                            textMessage.MessageCreator.Name,
                            textMessage.MessageCreator.AvatarPath,
                            textMessage.MessageCreator.RegisterDate
                        ) : null,
                        textMessage.IsDeleted,
                        textMessage.IsSystemMessage
                    );
                }

                // Проверяем в файловых сообщениях
                var fileMessage = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .FirstOrDefaultAsync(f => f.MessageId == messageId);

                if (fileMessage != null)
                {
                    var chat = await _context.Chats
                        .Include(c => c.Users)
                        .FirstOrDefaultAsync(c => c.Id == fileMessage.ChatId);

                    if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                        return null;

                    return new MessageResponseDTO(
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
                        fileMessage.IsDeleted,
                        fileMessage.IsSystemMessage,
                        fileMessage.FileName,
                        fileMessage.FileSize,
                        fileMessage.ContentType
                    );
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<List<MessageResponseDTO>> GetAllMessagesAsync(Guid currentUserId)
        {
            try
            {
                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} attempted to get all messages without permission", currentUserId);
                    return new List<MessageResponseDTO>();
                }

                var result = new List<MessageResponseDTO>();

                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .ToListAsync();

                foreach (var msg in textMessages)
                {
                    result.Add(new MessageResponseDTO(
                        msg.MessageId,
                        msg.MessageText,
                        msg.MessageCreateDate,
                        msg.MessageLastUpdateDate,
                        msg.UserId,
                        msg.ChatId,
                        msg.MessageCreator != null ? new UserResponseDTO(
                            msg.MessageCreator.Id,
                            msg.MessageCreator.Name,
                            msg.MessageCreator.AvatarPath,
                            msg.MessageCreator.RegisterDate
                        ) : null,
                        msg.IsDeleted,
                        msg.IsSystemMessage
                    ));
                }

                var fileMessages = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .ToListAsync();

                foreach (var fileMsg in fileMessages)
                {
                    result.Add(new MessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        fileMsg.MessageCreator != null ? new UserResponseDTO(
                            fileMsg.MessageCreator.Id,
                            fileMsg.MessageCreator.Name,
                            fileMsg.MessageCreator.AvatarPath,
                            fileMsg.MessageCreator.RegisterDate
                        ) : null,
                        fileMsg.IsDeleted,
                        fileMsg.IsSystemMessage,
                        fileMsg.FileName,
                        fileMsg.FileSize,
                        fileMsg.ContentType
                    ));
                }

                return result.OrderByDescending(m => m.MessageCreateDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all messages");
                return new List<MessageResponseDTO>();
            }
        }

        public async Task<List<MessageResponseDTO>> GetChatMessagesAsync(Guid chatId, Guid currentUserId, int page = 1, int pageSize = 50)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", currentUserId, chatId);
                    return new List<MessageResponseDTO>();
                }

                var result = new List<MessageResponseDTO>();

                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && !m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var msg in textMessages)
                {
                    result.Add(new MessageResponseDTO(
                        msg.MessageId,
                        msg.MessageText,
                        msg.MessageCreateDate,
                        msg.MessageLastUpdateDate,
                        msg.UserId,
                        msg.ChatId,
                        msg.MessageCreator != null ? new UserResponseDTO(
                            msg.MessageCreator.Id,
                            msg.MessageCreator.Name,
                            msg.MessageCreator.AvatarPath,
                            msg.MessageCreator.RegisterDate
                        ) : null,
                        msg.IsDeleted,
                        msg.IsSystemMessage
                    ));
                }

                var fileMessages = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .Where(f => f.ChatId == chatId && !f.IsDeleted)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var fileMsg in fileMessages)
                {
                    result.Add(new MessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        fileMsg.MessageCreator != null ? new UserResponseDTO(
                            fileMsg.MessageCreator.Id,
                            fileMsg.MessageCreator.Name,
                            fileMsg.MessageCreator.AvatarPath,
                            fileMsg.MessageCreator.RegisterDate
                        ) : null,
                        fileMsg.IsDeleted,
                        fileMsg.IsSystemMessage,
                        fileMsg.FileName,
                        fileMsg.FileSize,
                        fileMsg.ContentType
                    ));
                }

                return result.OrderByDescending(m => m.MessageCreateDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId}", chatId);
                return new List<MessageResponseDTO>();
            }
        }
    }
}
using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.BaseModels;  // для UserRole

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
                var message = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null) return null;

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == message.ChatId);

                if (chat == null || !chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in chat containing message {MessageId}", currentUserId, messageId);
                    return null;
                }

                return new MessageResponseDTO(
                    message.MessageId,
                    message.MessageText,
                    message.MessageCreateDate,
                    message.MessageLastUpdateDate,
                    message.UserId,
                    message.ChatId,
                    message.MessageCreator != null ? new UserResponseDTO(message.MessageCreator.Id, message.MessageCreator.Name, message.MessageCreator.AvatarPath, message.MessageCreator.RegisterDate) : null,
                    message.IsDeleted,
                    message.IsSystemMessage
                );
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

                return await _context.Messages
                    .Include(m => m.MessageCreator)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Select(m => new MessageResponseDTO(
                        m.MessageId,
                        m.MessageText,
                        m.MessageCreateDate,
                        m.MessageLastUpdateDate,
                        m.UserId,
                        m.ChatId,
                        m.MessageCreator != null ? new UserResponseDTO(m.MessageCreator.Id, m.MessageCreator.Name, m.MessageCreator.AvatarPath, m.MessageCreator.RegisterDate) : null,
                        m.IsDeleted,
                        m.IsSystemMessage
                    ))
                    .ToListAsync();
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

                return await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && !m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new MessageResponseDTO(
                        m.MessageId,
                        m.MessageText,
                        m.MessageCreateDate,
                        m.MessageLastUpdateDate,
                        m.UserId,
                        m.ChatId,
                        m.MessageCreator != null ? new UserResponseDTO(m.MessageCreator.Id, m.MessageCreator.Name, m.MessageCreator.AvatarPath, m.MessageCreator.RegisterDate) : null,
                        m.IsDeleted,
                        m.IsSystemMessage
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId}", chatId);
                return new List<MessageResponseDTO>();
            }
        }
    }
}
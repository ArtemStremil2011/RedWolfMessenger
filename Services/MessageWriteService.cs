using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Services
{
    public class MessageWriteService : IMessageWriteService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<MessageWriteService> _logger;

        public MessageWriteService(AppDBContext context, ILogger<MessageWriteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MessageResponseDTO?> CreateMessageAsync(Guid userId, Guid chatId, string text)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == userId))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", userId, chatId);
                    return null;
                }

                var message = new Message
                {
                    MessageText = text,
                    UserId = userId,
                    ChatId = chatId,
                    MessageCreateDate = DateTime.UtcNow,
                    MessageLastUpdateDate = DateTime.UtcNow,
                    IsDeleted = false,
                    IsSystemMessage = false
                };

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                await _context.Entry(message).Reference(m => m.MessageCreator).LoadAsync();

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
                _logger.LogError(ex, "Error creating message");
                return null;
            }
        }

        public async Task<MessageResponseDTO?> UpdateMessageAsync(Guid messageId, string newText, Guid currentUserId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Message {MessageId} not found", messageId);
                    return null;
                }

                if (message.IsDeleted)
                {
                    _logger.LogWarning("Message {MessageId} is deleted", messageId);
                    return null;
                }

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (message.UserId != currentUserId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} cannot edit message {MessageId}", currentUserId, messageId);
                    return null;
                }

                message.MessageText = newText;
                message.MessageLastUpdateDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

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
                _logger.LogError(ex, "Error updating message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null) return false;

                if (message.IsDeleted)
                {
                    _logger.LogWarning("Message {MessageId} already deleted", messageId);
                    return false;
                }

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (message.UserId != currentUserId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} cannot delete message {MessageId}", currentUserId, messageId);
                    return false;
                }

                message.IsDeleted = true;
                message.MessageLastUpdateDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message {MessageId} soft deleted", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return false;
            }
        }

        public async Task<bool> PermanentDeleteMessageAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null) return false;

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} attempted permanent delete without permission", currentUserId);
                    return false;
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message {MessageId} permanently deleted", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting message {MessageId}", messageId);
                return false;
            }
        }

        public async Task<bool> RestoreMessageAsync(Guid messageId, Guid currentUserId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null) return false;

                if (!message.IsDeleted)
                {
                    _logger.LogWarning("Message {MessageId} is not deleted", messageId);
                    return false;
                }

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (message.UserId != currentUserId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} cannot restore message {MessageId}", currentUserId, messageId);
                    return false;
                }

                message.IsDeleted = false;
                message.MessageLastUpdateDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message {MessageId} restored", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring message {MessageId}", messageId);
                return false;
            }
        }
    }
}
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
        private readonly IServerCryptoService _serverCryptoService;

        public MessageWriteService(
            AppDBContext context, 
            ILogger<MessageWriteService> logger,
            IServerCryptoService serverCryptoService)
        {
            _context = context;
            _logger = logger;
            _serverCryptoService = serverCryptoService;
        }

        // ============ ОБЫЧНЫЕ СООБЩЕНИЯ (БЕЗ ШИФРОВАНИЯ) ============
        
        public async Task<MessageResponseDTO?> CreateMessageAsync(Guid userId, Guid chatId, string text)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == userId))
                    return null;

                var now = DateTime.UtcNow;
                
                var message = new Message
                {
                    MessageText = text,
                    EncryptedData = null,
                    Iv = null,
                    UserId = userId,
                    ChatId = chatId,
                    MessageCreateDate = now,
                    MessageLastUpdateDate = now,
                    IsDeleted = false,
                    IsSystemMessage = false,
                    IsRead = false
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
                    message.IsSystemMessage,
                    message.IsRead,
                    message.EncryptedData,
                    message.Iv
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message");
                return null;
            }
        }

        // ============ ЗАШИФРОВАННЫЕ СООБЩЕНИЯ (E2EE ДЛЯ ПОЛЬЗОВАТЕЛЕЙ) ============
        
        public async Task<MessageResponseDTO?> CreateEncryptedMessageAsync(Guid userId, Guid chatId, string encryptedData, string iv)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == userId))
                    return null;

                var now = DateTime.UtcNow;
                
                var message = new Message
                {
                    MessageText = null,
                    EncryptedData = encryptedData,
                    Iv = iv,
                    UserId = userId,
                    ChatId = chatId,
                    MessageCreateDate = now,
                    MessageLastUpdateDate = now,
                    IsDeleted = false,
                    IsSystemMessage = false,
                    IsRead = false
                };

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                await _context.Entry(message).Reference(m => m.MessageCreator).LoadAsync();

                return new MessageResponseDTO(
                    message.MessageId,
                    null,
                    message.MessageCreateDate,
                    message.MessageLastUpdateDate,
                    message.UserId,
                    message.ChatId,
                    message.MessageCreator != null ? new UserResponseDTO(message.MessageCreator.Id, message.MessageCreator.Name, message.MessageCreator.AvatarPath, message.MessageCreator.RegisterDate) : null,
                    message.IsDeleted,
                    message.IsSystemMessage,
                    message.IsRead,
                    message.EncryptedData,
                    message.Iv
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating encrypted message");
                return null;
            }
        }

        // ============ ДВОЙНОЕ ШИФРОВАНИЕ (ДЛЯ СЕРВЕРА) ============
        
        public async Task<MessageResponseDTO?> CreateDualEncryptedMessageAsync(
            Guid userId, 
            Guid chatId, 
            string encryptedForUsers, 
            string ivForUsers,
            string encryptedForServer,
            string ivForServer)
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

                var now = DateTime.UtcNow;
                
                // 1. Сохраняем сообщение для пользователей (зашифрованное)
                var message = new Message
                {
                    MessageText = null,
                    EncryptedData = encryptedForUsers,
                    Iv = ivForUsers,
                    UserId = userId,
                    ChatId = chatId,
                    MessageCreateDate = now,
                    MessageLastUpdateDate = now,
                    IsDeleted = false,
                    IsSystemMessage = false,
                    IsRead = false
                };

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Dual encrypted message saved - Chat: {ChatId}, User: {UserId}, MessageId: {MessageId}", 
                    chatId, userId, message.MessageId);

                // 2. Расшифровываем серверную копию для модерации
                try
                {
                    if (_serverCryptoService.IsConfigured())
                    {
                        var plainText = await _serverCryptoService.DecryptMessageAsync(encryptedForServer, ivForServer);
                        
                        if (!string.IsNullOrEmpty(plainText))
                        {
                            _logger.LogInformation("Server decrypted message in chat {ChatId}: {Preview}", 
                                chatId, plainText.Length > 50 ? plainText[..50] + "..." : plainText);
                            
                            // Сохраняем расшифрованную копию для модерации
                            var moderatedMessage = new ModeratedMessage
                            {
                                MessageId = message.MessageId,
                                PlainText = plainText,
                                ChatId = chatId,
                                UserId = userId,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            await _context.ModeratedMessages.AddAsync(moderatedMessage);
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation("Moderated copy saved for message {MessageId}", message.MessageId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Server crypto not configured, skipping decryption for message {MessageId}", message.MessageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt or save server copy of message {MessageId}", message.MessageId);
                    // Не удаляем сообщение, просто логируем ошибку
                }

                await _context.Entry(message).Reference(m => m.MessageCreator).LoadAsync();

                return new MessageResponseDTO(
                    message.MessageId,
                    null,
                    message.MessageCreateDate,
                    message.MessageLastUpdateDate,
                    message.UserId,
                    message.ChatId,
                    message.MessageCreator != null ? new UserResponseDTO(message.MessageCreator.Id, message.MessageCreator.Name, message.MessageCreator.AvatarPath, message.MessageCreator.RegisterDate) : null,
                    message.IsDeleted,
                    message.IsSystemMessage,
                    message.IsRead,
                    message.EncryptedData,
                    message.Iv
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating dual encrypted message");
                return null;
            }
        }

        // ============ РЕДАКТИРОВАНИЕ СООБЩЕНИЙ ============
        
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
                    message.IsSystemMessage,
                    message.IsRead,
                    message.EncryptedData,
                    message.Iv
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<MessageResponseDTO?> UpdateEncryptedMessageAsync(Guid messageId, string encryptedData, string iv, Guid currentUserId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Encrypted message {MessageId} not found", messageId);
                    return null;
                }

                if (message.IsDeleted)
                {
                    _logger.LogWarning("Encrypted message {MessageId} is deleted", messageId);
                    return null;
                }

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (message.UserId != currentUserId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {UserId} cannot edit encrypted message {MessageId}", currentUserId, messageId);
                    return null;
                }

                message.EncryptedData = encryptedData;
                message.Iv = iv;
                message.MessageText = null;
                message.MessageLastUpdateDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Encrypted message {MessageId} updated by user {UserId}", messageId, currentUserId);

                return new MessageResponseDTO(
                    message.MessageId,
                    null,
                    message.MessageCreateDate,
                    message.MessageLastUpdateDate,
                    message.UserId,
                    message.ChatId,
                    message.MessageCreator != null ? new UserResponseDTO(message.MessageCreator.Id, message.MessageCreator.Name, message.MessageCreator.AvatarPath, message.MessageCreator.RegisterDate) : null,
                    message.IsDeleted,
                    message.IsSystemMessage,
                    message.IsRead,
                    message.EncryptedData,
                    message.Iv
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating encrypted message {MessageId}", messageId);
                return null;
            }
        }

        // ============ УПРАВЛЕНИЕ СООБЩЕНИЯМИ ============
        
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

                // Также удаляем запись из модерации, если есть
                var moderated = await _context.ModeratedMessages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);
                if (moderated != null)
                {
                    _context.ModeratedMessages.Remove(moderated);
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

        public async Task MarkMessagesAsReadAsync(Guid chatId, Guid userId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => m.ChatId == chatId && m.UserId != userId && !m.IsRead)
                    .ToListAsync();
                
                foreach (var msg in messages)
                {
                    msg.IsRead = true;
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Messages marked as read in chat {ChatId} for user {UserId}", chatId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in chat {ChatId}", chatId);
            }
        }
    }
}
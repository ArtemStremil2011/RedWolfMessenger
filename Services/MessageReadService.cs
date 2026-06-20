using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
                // Проверяем текстовые сообщения
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

                    var creator = textMessage.MessageCreator != null
                        ? new UserResponseDTO(
                            textMessage.MessageCreator.Id,
                            textMessage.MessageCreator.Name ?? "",
                            textMessage.MessageCreator.AvatarPath ?? "",
                            textMessage.MessageCreator.RegisterDate,
                            textMessage.MessageCreator.PublicKey
                        )
                        : null;

                    return new MessageResponseDTO(
                        textMessage.MessageId,
                        textMessage.MessageText,
                        textMessage.MessageCreateDate,
                        textMessage.MessageLastUpdateDate,
                        textMessage.UserId,
                        textMessage.ChatId,
                        creator,
                        textMessage.IsDeleted,
                        textMessage.IsSystemMessage,
                        textMessage.IsRead,
                        textMessage.EncryptedData,
                        textMessage.Iv
                    );
                }

                // Проверяем файловые сообщения
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

                    var creator = fileMessage.MessageCreator != null
                        ? new UserResponseDTO(
                            fileMessage.MessageCreator.Id,
                            fileMessage.MessageCreator.Name ?? "",
                            fileMessage.MessageCreator.AvatarPath ?? "",
                            fileMessage.MessageCreator.RegisterDate,
                            fileMessage.MessageCreator.PublicKey
                        )
                        : null;

                    return new MessageResponseDTO(
                        fileMessage.MessageId,
                        fileMessage.MessageText,
                        fileMessage.MessageCreateDate,
                        fileMessage.MessageLastUpdateDate,
                        fileMessage.UserId,
                        fileMessage.ChatId,
                        creator,
                        fileMessage.IsDeleted,
                        fileMessage.IsSystemMessage,
                        fileMessage.IsRead,
                        fileMessage.EncryptedData,
                        fileMessage.Iv,
                        fileMessage.FileName,
                        fileMessage.FileSize,
                        fileMessage.ContentType,
                        fileMessage.Duration
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
                    return new List<MessageResponseDTO>();
                }

                var result = new List<MessageResponseDTO>();

                // Текстовые сообщения
                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .ToListAsync();

                foreach (var msg in textMessages)
                {
                    var creator = msg.MessageCreator != null
                        ? new UserResponseDTO(
                            msg.MessageCreator.Id,
                            msg.MessageCreator.Name ?? "",
                            msg.MessageCreator.AvatarPath ?? "",
                            msg.MessageCreator.RegisterDate,
                            msg.MessageCreator.PublicKey
                        )
                        : null;

                    result.Add(new MessageResponseDTO(
                        msg.MessageId,
                        msg.MessageText,
                        msg.MessageCreateDate,
                        msg.MessageLastUpdateDate,
                        msg.UserId,
                        msg.ChatId,
                        creator,
                        msg.IsDeleted,
                        msg.IsSystemMessage,
                        msg.IsRead,
                        msg.EncryptedData,
                        msg.Iv
                    ));
                }

                // Файловые сообщения
                var fileMessages = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .ToListAsync();

                foreach (var fileMsg in fileMessages)
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

                    result.Add(new MessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        creator,
                        fileMsg.IsDeleted,
                        fileMsg.IsSystemMessage,
                        fileMsg.IsRead,
                        fileMsg.EncryptedData,
                        fileMsg.Iv,
                        fileMsg.FileName,
                        fileMsg.FileSize,
                        fileMsg.ContentType,
                        fileMsg.Duration
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
                    return new List<MessageResponseDTO>();
                }

                var result = new List<MessageResponseDTO>();

                // Текстовые сообщения
                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && !m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var msg in textMessages)
                {
                    var creator = msg.MessageCreator != null
                        ? new UserResponseDTO(
                            msg.MessageCreator.Id,
                            msg.MessageCreator.Name ?? "",
                            msg.MessageCreator.AvatarPath ?? "",
                            msg.MessageCreator.RegisterDate,
                            msg.MessageCreator.PublicKey
                        )
                        : null;

                    result.Add(new MessageResponseDTO(
                        msg.MessageId,
                        msg.MessageText,
                        msg.MessageCreateDate,
                        msg.MessageLastUpdateDate,
                        msg.UserId,
                        msg.ChatId,
                        creator,
                        msg.IsDeleted,
                        msg.IsSystemMessage,
                        msg.IsRead,
                        msg.EncryptedData,
                        msg.Iv
                    ));
                }

                // Файловые сообщения (включая голосовые)
                var fileMessages = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .Where(f => f.ChatId == chatId && !f.IsDeleted)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var fileMsg in fileMessages)
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

                    result.Add(new MessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        creator,
                        fileMsg.IsDeleted,
                        fileMsg.IsSystemMessage,
                        fileMsg.IsRead,
                        fileMsg.EncryptedData,
                        fileMsg.Iv,
                        fileMsg.FileName,
                        fileMsg.FileSize,
                        fileMsg.ContentType,
                        fileMsg.Duration
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

        public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid userId)
        {
            try
            {
                var chats = await _context.Chats
                    .Where(c => c.Users.Any(u => u.Id == userId))
                    .Select(c => c.Id)
                    .ToListAsync();

                var counts = new Dictionary<Guid, int>();
                foreach (var chatId in chats)
                {
                    var count = await _context.Messages
                        .CountAsync(m => m.ChatId == chatId && m.UserId != userId && !m.IsRead);
                    counts[chatId] = count;
                }
                return counts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread counts for user {UserId}", userId);
                return new Dictionary<Guid, int>();
            }
        }

        public async Task<List<MessageResponseDTO>> GetDeletedMessagesAsync(Guid chatId, Guid userId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null || !chat.Users.Any(u => u.Id == userId))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", userId, chatId);
                    return new List<MessageResponseDTO>();
                }

                var result = new List<MessageResponseDTO>();

                // Текстовые сообщения в корзине
                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && m.UserId == userId && m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .ToListAsync();

                foreach (var msg in textMessages)
                {
                    var creator = msg.MessageCreator != null
                        ? new UserResponseDTO(
                            msg.MessageCreator.Id,
                            msg.MessageCreator.Name ?? "",
                            msg.MessageCreator.AvatarPath ?? "",
                            msg.MessageCreator.RegisterDate,
                            msg.MessageCreator.PublicKey
                        )
                        : null;

                    result.Add(new MessageResponseDTO(
                        msg.MessageId,
                        msg.MessageText,
                        msg.MessageCreateDate,
                        msg.MessageLastUpdateDate,
                        msg.UserId,
                        msg.ChatId,
                        creator,
                        msg.IsDeleted,
                        msg.IsSystemMessage,
                        msg.IsRead,
                        msg.EncryptedData,
                        msg.Iv
                    ));
                }

                // Файловые сообщения в корзине (включая голосовые)
                var fileMessages = await _context.Set<FileMessage>()
                    .Include(f => f.MessageCreator)
                    .Where(f => f.ChatId == chatId && f.UserId == userId && f.IsDeleted)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .ToListAsync();

                foreach (var fileMsg in fileMessages)
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

                    result.Add(new MessageResponseDTO(
                        fileMsg.MessageId,
                        fileMsg.MessageText,
                        fileMsg.MessageCreateDate,
                        fileMsg.MessageLastUpdateDate,
                        fileMsg.UserId,
                        fileMsg.ChatId,
                        creator,
                        fileMsg.IsDeleted,
                        fileMsg.IsSystemMessage,
                        fileMsg.IsRead,
                        fileMsg.EncryptedData,
                        fileMsg.Iv,
                        fileMsg.FileName,
                        fileMsg.FileSize,
                        fileMsg.ContentType,
                        fileMsg.Duration
                    ));
                }

                return result.OrderByDescending(m => m.MessageCreateDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deleted messages for chat {ChatId}", chatId);
                return new List<MessageResponseDTO>();
            }
        }
    }
}
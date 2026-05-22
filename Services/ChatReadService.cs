using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;

namespace Messenger.Services
{
    public class ChatReadService : IChatReadService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<ChatReadService> _logger;

        public ChatReadService(AppDBContext context, ILogger<ChatReadService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ChatResponseDTO>> GetAllChatsAsync(Guid currentUserId)
        {
            try
            {
                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    return new List<ChatResponseDTO>();
                }

                return await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Select(c => new ChatResponseDTO(
                        c.Id,
                        c.ChatName,
                        c.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                        null,
                        c.MaxUsers,
                        c.CreatedAt,
                        c.LastActivityAt
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all chats");
                return new List<ChatResponseDTO>();
            }
        }

        public async Task<ChatResponseDTO?> GetChatAsync(Guid chatId, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null) return null;
                if (!chat.Users.Any(u => u.Id == currentUserId)) return null;

                var otherUser = chat.MaxUsers == 2 && chat.Users.Count == 2
                    ? chat.Users.FirstOrDefault(u => u.Id != currentUserId)
                    : null;

                return new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                    otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
                    chat.MaxUsers,
                    chat.CreatedAt,
                    chat.LastActivityAt
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat {ChatId}", chatId);
                return null;
            }
        }

        public async Task<List<ChatResponseDTO>> GetUserChatsAsync(Guid userId, Guid currentUserId)
        {
            try
            {
                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUserId != userId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    return new List<ChatResponseDTO>();
                }

                var chats = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Where(c => c.Users.Any(u => u.Id == userId))
                    .ToListAsync();

                return chats.Select(chat =>
                {
                    var otherUser = chat.MaxUsers == 2 && chat.Users.Count == 2
                        ? chat.Users.FirstOrDefault(u => u.Id != userId)
                        : null;

                    return new ChatResponseDTO(
                        chat.Id,
                        chat.ChatName,
                        chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                        otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
                        chat.MaxUsers,
                        chat.CreatedAt,
                        chat.LastActivityAt
                    );
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chats for user {UserId}", userId);
                return new List<ChatResponseDTO>();
            }
        }

        // ========== ГЛАВНЫЙ МЕТОД - РАБОТАЕТ БЕЗ DISCRIMINATOR ==========
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

                // 1. Получаем текстовые сообщения из таблицы Messages
                var textMessages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && !m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.MessageText,
                        m.MessageCreateDate,
                        m.MessageLastUpdateDate,
                        m.UserId,
                        m.ChatId,
                        m.IsDeleted,
                        m.IsSystemMessage,
                        CreatorName = m.MessageCreator != null ? m.MessageCreator.Name : "Unknown",
                        CreatorId = m.MessageCreator != null ? m.MessageCreator.Id : Guid.Empty,
                        CreatorAvatar = m.MessageCreator != null ? m.MessageCreator.AvatarPath : null,
                        CreatorRegisterDate = m.MessageCreator != null ? m.MessageCreator.RegisterDate : DateTime.MinValue,
                        IsFile = false,
                        FileName = (string?)null,
                        FileSize = (long?)null,
                        ContentType = (string?)null
                    })
                    .ToListAsync();

                // 2. Получаем файловые сообщения напрямую через SQL (обход EF Core)
                var fileMessages = await _context.FileMessages
                    .Include(f => f.MessageCreator)
                    .Where(f => f.ChatId == chatId && !f.IsDeleted)
                    .OrderByDescending(f => f.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new
                    {
                        f.MessageId,
                        f.MessageText,
                        f.MessageCreateDate,
                        f.MessageLastUpdateDate,
                        f.UserId,
                        f.ChatId,
                        f.IsDeleted,
                        f.IsSystemMessage,
                        CreatorName = f.MessageCreator != null ? f.MessageCreator.Name : "Unknown",
                        CreatorId = f.MessageCreator != null ? f.MessageCreator.Id : Guid.Empty,
                        CreatorAvatar = f.MessageCreator != null ? f.MessageCreator.AvatarPath : null,
                        CreatorRegisterDate = f.MessageCreator != null ? f.MessageCreator.RegisterDate : DateTime.MinValue,
                        IsFile = true,
                        FileName = f.FileName,
                        FileSize = f.FileSize,
                        ContentType = f.ContentType
                    })
                    .ToListAsync();

                // 3. Объединяем и сортируем
                var allMessages = new List<dynamic>();
                allMessages.AddRange(textMessages);
                allMessages.AddRange(fileMessages);

                var sorted = allMessages
                    .OrderByDescending(m => m.MessageCreateDate)
                    .ToList();

                // 4. Конвертируем в DTO
                foreach (var msg in sorted)
                {
                    var creator = new UserResponseDTO(
                        msg.CreatorId,
                        msg.CreatorName,
                        msg.CreatorAvatar,
                        msg.CreatorRegisterDate
                    );

                    if (msg.IsFile)
                    {
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
                            msg.FileName,
                            msg.FileSize,
                            msg.ContentType
                        ));
                    }
                    else
                    {
                        result.Add(new MessageResponseDTO(
                            msg.MessageId,
                            msg.MessageText,
                            msg.MessageCreateDate,
                            msg.MessageLastUpdateDate,
                            msg.UserId,
                            msg.ChatId,
                            creator,
                            msg.IsDeleted,
                            msg.IsSystemMessage
                        ));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId}", chatId);
                return new List<MessageResponseDTO>();
            }
        }

        public async Task<int> GetTotalMessagesCountAsync(Guid chatId)
        {
            try
            {
                var textCount = await _context.Messages.CountAsync(m => m.ChatId == chatId && !m.IsDeleted);
                var fileCount = await _context.FileMessages.CountAsync(f => f.ChatId == chatId && !f.IsDeleted);
                return textCount + fileCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message count for chat {ChatId}", chatId);
                return 0;
            }
        }

        public async Task<bool> UserInChatAsync(Guid chatId, Guid userId)
        {
            try
            {
                return await _context.Chats
                    .AnyAsync(c => c.Id == chatId && c.Users.Any(u => u.Id == userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} in chat {ChatId}", userId, chatId);
                return false;
            }
        }

        public async Task<UserStatusDTO> GetUserStatusAsync(Guid userId)
        {
            return new UserStatusDTO { UserId = userId, IsOnline = false, LastSeen = null };
        }

        public async Task<UserResponseDTO?> GetUserProfileForChatAsync(Guid userId, Guid currentUserId)
        {
            try
            {
                var commonChat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c =>
                        c.Users.Any(u => u.Id == userId) &&
                        c.Users.Any(u => u.Id == currentUserId));

                if (commonChat == null && userId != currentUserId)
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId);
                    if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                    {
                        return null;
                    }
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return null;

                return new UserResponseDTO(
                    user.Id,
                    user.Name,
                    user.AvatarPath,
                    user.RegisterDate
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile {UserId}", userId);
                return null;
            }
        }
    }
}
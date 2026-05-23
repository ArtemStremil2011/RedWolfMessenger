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
                        c.LastActivityAt,
                        c.MaxUsers > 2 ? c.AvatarPath : null
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
                    chat.LastActivityAt,
                    chat.MaxUsers > 2 ? chat.AvatarPath : null
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
                        chat.LastActivityAt,
                        chat.MaxUsers > 2 ? chat.AvatarPath : null
                    );
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chats for user {UserId}", userId);
                return new List<ChatResponseDTO>();
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

                var messages = await _context.Messages
                    .Include(m => m.MessageCreator)
                    .Where(m => m.ChatId == chatId && !m.IsDeleted)
                    .OrderByDescending(m => m.MessageCreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var msg in messages)
                {
                    var creator = msg.MessageCreator != null
                        ? new UserResponseDTO(msg.MessageCreator.Id, msg.MessageCreator.Name, msg.MessageCreator.AvatarPath, msg.MessageCreator.RegisterDate)
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
                        null,
                        null,
                        null
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
                    var creator = fileMsg.MessageCreator != null
                        ? new UserResponseDTO(fileMsg.MessageCreator.Id, fileMsg.MessageCreator.Name, fileMsg.MessageCreator.AvatarPath, fileMsg.MessageCreator.RegisterDate)
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

        public async Task<int> GetTotalMessagesCountAsync(Guid chatId)
        {
            try
            {
                var textCount = await _context.Messages.CountAsync(m => m.ChatId == chatId && !m.IsDeleted);
                var fileCount = await _context.Set<FileMessage>().CountAsync(f => f.ChatId == chatId && !f.IsDeleted);
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

        public async Task<string?> GetGroupAvatarPathAsync(Guid chatId, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId && c.MaxUsers > 2);

                if (chat == null)
                    return null;

                if (!chat.Users.Any(u => u.Id == currentUserId))
                    return null;

                return string.IsNullOrEmpty(chat.AvatarPath) ? null : chat.AvatarPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group avatar for chat {ChatId}", chatId);
                return null;
            }
        }
    }
}
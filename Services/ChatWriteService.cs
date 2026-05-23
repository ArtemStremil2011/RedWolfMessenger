using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Services
{
    public class ChatWriteService : IChatWriteService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<ChatWriteService> _logger;
        private readonly IWebHostEnvironment _environment;

        public ChatWriteService(
            AppDBContext context,
            ILogger<ChatWriteService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        public async Task<ChatResponseDTO?> CreateChatAsync(CreateChatDTO dto, Guid currentUserId)
        {
            try
            {
                if (!dto.MemberIds.Contains(currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in member list", currentUserId);
                    return null;
                }

                var memberIds = dto.MemberIds.Distinct().ToList();

                if (memberIds.Count < 2)
                {
                    _logger.LogWarning("Member count less than 2");
                    return null;
                }

                var maxUsers = dto.MaxUsers ?? memberIds.Count;
                if (memberIds.Count > maxUsers)
                {
                    _logger.LogWarning("Member count exceeds MaxUsers");
                    return null;
                }

                var users = new List<User>();
                foreach (var userId in memberIds)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        _logger.LogWarning("User {UserId} not found", userId);
                        return null;
                    }
                    users.Add(user);
                }

                bool isPrivateChat = memberIds.Count == 2 && maxUsers == 2;
                if (isPrivateChat)
                {
                    var existingChat = await _context.Chats
                        .Include(c => c.Users)
                        .FirstOrDefaultAsync(c => c.Users.Count == 2 &&
                            c.Users.All(u => memberIds.Contains(u.Id)) &&
                            c.MaxUsers == 2);

                    if (existingChat != null)
                    {
                        var otherUser = existingChat.Users.FirstOrDefault(u => u.Id != currentUserId);
                        return new ChatResponseDTO(
                            existingChat.Id,
                            existingChat.ChatName,
                            existingChat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                            otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
                            existingChat.MaxUsers,
                            existingChat.CreatedAt,
                            existingChat.LastActivityAt,
                            null
                        );
                    }
                }

                string chatName = dto.ChatName;
                if (string.IsNullOrEmpty(chatName))
                {
                    if (isPrivateChat)
                    {
                        chatName = $"{users[0].Name} & {users[1].Name}";
                    }
                    else
                    {
                        var firstNames = users.Take(3).Select(u => u.Name);
                        chatName = $"Group of {string.Join(", ", firstNames)}" + (users.Count > 3 ? "..." : "");
                    }
                }

                var chat = new Chat
                {
                    ChatName = chatName,
                    MaxUsers = maxUsers,
                    IsPrivate = true,
                    CreatedById = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    AvatarPath = null
                };

                foreach (var user in users)
                {
                    chat.Users.Add(user);
                }

                await _context.Chats.AddAsync(chat);
                await _context.SaveChangesAsync();

                await _context.Entry(chat).Collection(c => c.Users).LoadAsync();

                if (!isPrivateChat)
                {
                    var creator = users.First(u => u.Id == currentUserId);
                    var systemMessage = new Message
                    {
                        MessageText = $"{creator.Name} created group \"{chat.ChatName}\"",
                        UserId = currentUserId,
                        ChatId = chat.Id,
                        IsSystemMessage = true,
                        MessageCreateDate = DateTime.UtcNow,
                        MessageLastUpdateDate = DateTime.UtcNow
                    };
                    await _context.Messages.AddAsync(systemMessage);
                    await _context.SaveChangesAsync();
                }

                var responseOtherUser = isPrivateChat ? users.FirstOrDefault(u => u.Id != currentUserId) : null;

                return new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                    responseOtherUser != null ? new UserResponseDTO(responseOtherUser.Id, responseOtherUser.Name, responseOtherUser.AvatarPath, responseOtherUser.RegisterDate) : null,
                    chat.MaxUsers,
                    chat.CreatedAt,
                    chat.LastActivityAt,
                    null
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat");
                return null;
            }
        }

        public async Task<bool> UpdateChatNameAsync(Guid chatId, string newName, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
                if (chat == null) return false;

                if (chat.CreatedById != currentUserId)
                {
                    _logger.LogWarning("User {UserId} attempted to rename chat {ChatId} without permission", currentUserId, chatId);
                    return false;
                }

                chat.ChatName = newName;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Chat {ChatId} renamed to {NewName}", chatId, newName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> AddUserToChatAsync(Guid chatId, Guid userIdToAdd, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null) return false;

                if (chat.CreatedById != currentUserId)
                {
                    _logger.LogWarning("User {UserId} attempted to add user to chat {ChatId} without permission", currentUserId, chatId);
                    return false;
                }

                var userToAdd = await _context.Users.FindAsync(userIdToAdd);
                if (userToAdd == null) return false;

                if (chat.Users.Any(u => u.Id == userIdToAdd))
                {
                    _logger.LogWarning("User {UserId} already in chat {ChatId}", userIdToAdd, chatId);
                    return false;
                }

                if (chat.Users.Count >= chat.MaxUsers)
                {
                    _logger.LogWarning("Chat {ChatId} already has max users {MaxUsers}", chatId, chat.MaxUsers);
                    return false;
                }

                chat.Users.Add(userToAdd);
                chat.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var currentUser = await _context.Users.FindAsync(currentUserId);
                var systemMessage = new Message
                {
                    MessageText = $"{currentUser?.Name} added {userToAdd.Name} to group",
                    UserId = currentUserId,
                    ChatId = chat.Id,
                    IsSystemMessage = true,
                    MessageCreateDate = DateTime.UtcNow,
                    MessageLastUpdateDate = DateTime.UtcNow
                };
                await _context.Messages.AddAsync(systemMessage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> RemoveUserFromChatAsync(Guid chatId, Guid userIdToRemove, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null) return false;

                var userToRemove = await _context.Users.FindAsync(userIdToRemove);
                if (userToRemove == null) return false;

                if (!chat.Users.Any(u => u.Id == userIdToRemove))
                {
                    _logger.LogWarning("User {UserId} not in chat {ChatId}", userIdToRemove, chatId);
                    return false;
                }

                bool isCreator = chat.CreatedById == currentUserId;
                bool isSelf = currentUserId == userIdToRemove;

                if (!isCreator && !isSelf)
                {
                    _logger.LogWarning("User {UserId} cannot remove user {TargetUserId} from chat {ChatId}", currentUserId, userIdToRemove, chatId);
                    return false;
                }

                if (chat.CreatedById == userIdToRemove && !isSelf)
                {
                    _logger.LogWarning("Cannot remove creator from chat {ChatId}", chatId);
                    return false;
                }

                chat.Users.Remove(userToRemove);
                chat.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var currentUser = await _context.Users.FindAsync(currentUserId);
                var systemMessage = new Message
                {
                    MessageText = isSelf
                        ? $"{userToRemove.Name} left the group"
                        : $"{currentUser?.Name} removed {userToRemove.Name} from group",
                    UserId = currentUserId,
                    ChatId = chat.Id,
                    IsSystemMessage = true,
                    MessageCreateDate = DateTime.UtcNow,
                    MessageLastUpdateDate = DateTime.UtcNow
                };
                await _context.Messages.AddAsync(systemMessage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> DeleteChatAsync(Guid chatId, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null) return false;

                if (chat.CreatedById != currentUserId)
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId);
                    if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                    {
                        _logger.LogWarning("User {UserId} cannot delete chat {ChatId}", currentUserId, chatId);
                        return false;
                    }
                }

                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Chat {ChatId} deleted", chatId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> LeaveGroupAsync(Guid chatId, Guid currentUserId)
        {
            return await RemoveUserFromChatAsync(chatId, currentUserId, currentUserId);
        }

        public async Task<string?> UploadGroupAvatarAsync(Guid chatId, IFormFile file, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId && c.MaxUsers > 2);

                if (chat == null)
                {
                    _logger.LogWarning("Group {ChatId} not found", chatId);
                    return null;
                }

                if (!chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning("User {UserId} not in group {ChatId}", currentUserId, chatId);
                    return null;
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension {Extension} for group avatar", extension);
                    return null;
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    _logger.LogWarning("File too large {Size} bytes for group avatar", file.Length);
                    return null;
                }

                var uploadPath = Path.Combine(_environment.WebRootPath, "group-avatars");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                if (!string.IsNullOrEmpty(chat.AvatarPath) && !chat.AvatarPath.Contains("default-group"))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, chat.AvatarPath.TrimStart('/'));
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                var fileName = $"group_{chatId}_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);
                var relativePath = $"/group-avatars/{fileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                chat.AvatarPath = relativePath;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Group avatar uploaded for chat {ChatId}", chatId);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading group avatar for chat {ChatId}", chatId);
                return null;
            }
        }

        public async Task<bool> DeleteGroupAvatarAsync(Guid chatId, Guid currentUserId)
        {
            try
            {
                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId && c.MaxUsers > 2);

                if (chat == null)
                    return false;

                if (!chat.Users.Any(u => u.Id == currentUserId))
                    return false;

                if (!string.IsNullOrEmpty(chat.AvatarPath))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, chat.AvatarPath.TrimStart('/'));
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                chat.AvatarPath = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Group avatar deleted for chat {ChatId}", chatId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group avatar for chat {ChatId}", chatId);
                return false;
            }
        }
    }
}
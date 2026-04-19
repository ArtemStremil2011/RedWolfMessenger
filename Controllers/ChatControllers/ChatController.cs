using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace Messenger.Controllers.ChatControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly ILogger<ChatController> _logger;

        public ChatController(AppDBContext context, ILogger<ChatController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==========================================
        // ВСЕ ЧАТЫ (только администраторам)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetAllChats()
        {
            try
            {
                _logger.LogInformation("GetAllChats called");
                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.Users.FindAsync(currentUserId);

                if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to access all chats");
                    return Forbid("Только администраторы могут просматривать все чаты");
                }

                var chats = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Select(c => new ChatResponseDTO(
                        c.Id,
                        c.ChatName,
                        c.Users.Select(u => new UserResponseDTO(
                            u.Id,
                            u.Name,
                            u.AvatarPath,
                            u.RegisterDate
                        )).ToList(),
                        null,
                        c.MaxUsers,
                        c.CreatedAt,
                        c.LastActivityAt
                    ))
                    .ToListAsync();

                _logger.LogInformation($"GetAllChats returned {chats.Count} chats");
                return Ok(chats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllChats");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // КОНКРЕТНЫЙ ЧАТ
        // ==========================================

        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(Guid chatId)
        {
            try
            {
                _logger.LogInformation($"GetChat called with chatId: {chatId}");
                var currentUserId = GetCurrentUserId();

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    _logger.LogWarning($"Chat with Id {chatId} not found");
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                var isUserInChat = chat.Users.Any(u => u.Id == currentUserId);
                if (!isUserInChat)
                {
                    _logger.LogWarning($"User {currentUserId} not in chat {chatId}");
                    return Forbid("Вы не имеете доступа к этому чату");
                }

                var otherUser = chat.MaxUsers == 2 && chat.Users.Count == 2
                    ? chat.Users.FirstOrDefault(u => u.Id != currentUserId)
                    : null;

                var response = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(
                        u.Id,
                        u.Name,
                        u.AvatarPath,
                        u.RegisterDate
                    )).ToList(),
                    otherUser != null ? new UserResponseDTO(
                        otherUser.Id,
                        otherUser.Name,
                        otherUser.AvatarPath,
                        otherUser.RegisterDate
                    ) : null,
                    chat.MaxUsers,
                    chat.CreatedAt,
                    chat.LastActivityAt
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetChat for chatId: {chatId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // СПИСОК ЧАТОВ ПОЛЬЗОВАТЕЛЯ
        // ==========================================

        [HttpGet("user-chats/{userId}")]
        public async Task<IActionResult> GetUserChats(Guid userId)
        {
            try
            {
                _logger.LogInformation($"GetUserChats called for userId: {userId}");
                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.Users.FindAsync(currentUserId);

                if (currentUserId != userId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to view chats of {userId}");
                    return Forbid("Вы можете просматривать только свои чаты");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found");
                    return NotFound($"Пользователь с Id {userId} не найден");
                }

                var chats = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Where(c => c.Users.Any(u => u.Id == userId))
                    .ToListAsync();

                var result = chats.Select(chat =>
                {
                    var otherUser = chat.MaxUsers == 2 && chat.Users.Count == 2
                        ? chat.Users.FirstOrDefault(u => u.Id != userId)
                        : null;

                    return new ChatResponseDTO(
                        chat.Id,
                        chat.ChatName,
                        chat.Users.Select(u => new UserResponseDTO(
                            u.Id,
                            u.Name,
                            u.AvatarPath,
                            u.RegisterDate
                        )).ToList(),
                        otherUser != null ? new UserResponseDTO(
                            otherUser.Id,
                            otherUser.Name,
                            otherUser.AvatarPath,
                            otherUser.RegisterDate
                        ) : null,
                        chat.MaxUsers,
                        chat.CreatedAt,
                        chat.LastActivityAt
                    );
                }).ToList();

                _logger.LogInformation($"GetUserChats returned {result.Count} chats for user {userId}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetUserChats for userId: {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // СООБЩЕНИЯ ЧАТА
        // ==========================================

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetChatMessages(Guid chatId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogInformation($"GetChatMessages called for chatId: {chatId}");
                var currentUserId = GetCurrentUserId();

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    _logger.LogWarning($"Chat {chatId} not found");
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                if (!chat.Users.Any(u => u.Id == currentUserId))
                {
                    _logger.LogWarning($"User {currentUserId} not in chat {chatId}");
                    return Forbid("Вы не имеете доступа к этому чату");
                }

                var messages = await _context.Messages
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
                        m.MessageCreator != null ? new UserResponseDTO(
                            m.MessageCreator.Id,
                            m.MessageCreator.Name,
                            m.MessageCreator.AvatarPath,
                            m.MessageCreator.RegisterDate
                        ) : null,
                        m.IsDeleted
                    ))
                    .ToListAsync();

                var total = await _context.Messages.CountAsync(m => m.ChatId == chatId && !m.IsDeleted);

                return Ok(new
                {
                    page,
                    pageSize,
                    total,
                    messages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetChatMessages for chatId: {chatId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // СОЗДАНИЕ ЧАТА (УНИВЕРСАЛЬНОЕ)
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDTO createChatDto)
        {
            try
            {
                _logger.LogInformation($"CreateChat called with {createChatDto.MemberIds?.Count ?? 0} members");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state");
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                _logger.LogDebug($"Current user ID: {currentUserId}");

                // Проверяем, что текущий пользователь в списке
                if (!createChatDto.MemberIds.Contains(currentUserId))
                {
                    _logger.LogWarning($"User {currentUserId} not in member list");
                    return BadRequest("Вы должны быть в списке участников");
                }

                // Убираем дубликаты
                var memberIds = createChatDto.MemberIds.Distinct().ToList();

                if (memberIds.Count < 2)
                {
                    _logger.LogWarning("Member count < 2");
                    return BadRequest("В чате должно быть минимум 2 участника");
                }

                var maxUsers = createChatDto.MaxUsers ?? memberIds.Count;
                if (memberIds.Count > maxUsers)
                {
                    _logger.LogWarning($"Member count {memberIds.Count} > MaxUsers {maxUsers}");
                    return BadRequest($"Количество участников ({memberIds.Count}) превышает MaxUsers ({maxUsers})");
                }

                // Получаем всех пользователей
                var users = new List<User>();
                foreach (var userId in memberIds)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        _logger.LogWarning($"User {userId} not found");
                        return BadRequest($"Пользователь {userId} не найден");
                    }
                    users.Add(user);
                }

                // Для личного чата (2 участника) проверяем, не существует ли уже
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
                        _logger.LogInformation($"Existing private chat found: {existingChat.Id}");

                        var otherUser = existingChat.Users.FirstOrDefault(u => u.Id != currentUserId);

                        return Ok(new ChatResponseDTO(
                            existingChat.Id,
                            existingChat.ChatName,
                            existingChat.Users.Select(u => new UserResponseDTO(
                                u.Id,
                                u.Name,
                                u.AvatarPath,
                                u.RegisterDate
                            )).ToList(),
                            otherUser != null ? new UserResponseDTO(
                                otherUser.Id,
                                otherUser.Name,
                                otherUser.AvatarPath,
                                otherUser.RegisterDate
                            ) : null,
                            existingChat.MaxUsers,
                            existingChat.CreatedAt,
                            existingChat.LastActivityAt
                        ));
                    }
                }

                // Генерируем название
                string chatName = createChatDto.ChatName;
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

                // Создаём чат
                var chat = new Chat
                {
                    ChatName = chatName,
                    MaxUsers = maxUsers,
                    IsPrivate = true,
                    CreatedById = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                };

                foreach (var user in users)
                {
                    chat.Users.Add(user);
                }

                await _context.Chats.AddAsync(chat);
                await _context.SaveChangesAsync();

                await _context.Entry(chat)
                    .Collection(c => c.Users)
                    .LoadAsync();

                _logger.LogInformation($"Chat created: {chat.Id} with {chat.Users.Count} members");

                // Системное сообщение для группы
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

                var responseOtherUser = isPrivateChat
                    ? users.FirstOrDefault(u => u.Id != currentUserId)
                    : null;

                var response = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(
                        u.Id,
                        u.Name,
                        u.AvatarPath,
                        u.RegisterDate
                    )).ToList(),
                    responseOtherUser != null ? new UserResponseDTO(
                        responseOtherUser.Id,
                        responseOtherUser.Name,
                        responseOtherUser.AvatarPath,
                        responseOtherUser.RegisterDate
                    ) : null,
                    chat.MaxUsers,
                    chat.CreatedAt,
                    chat.LastActivityAt
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateChat");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // УДАЛЕНИЕ ЧАТА
        // ==========================================

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            try
            {
                _logger.LogInformation($"DeleteChat called for chatId: {id}");
                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.Users.FindAsync(currentUserId);

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.MessagesHistory)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (chat == null)
                {
                    _logger.LogWarning($"Chat {id} not found");
                    return NotFound($"Чат с Id {id} не найден");
                }

                var isUserInChat = chat.Users.Any(u => u.Id == currentUserId);

                if (!isUserInChat && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning($"User {currentUserId} no permission to delete chat {id}");
                    return Forbid("Вы не имеете права удалять этот чат");
                }

                if (chat.MessagesHistory != null && chat.MessagesHistory.Any())
                {
                    _logger.LogInformation($"Deleting {chat.MessagesHistory.Count} messages");
                    _context.Messages.RemoveRange(chat.MessagesHistory);
                }

                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Chat {id} deleted");
                return Ok(new { message = "Чат успешно удалён", id = chat.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeleteChat for chatId: {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ==========================================

        private Guid GetCurrentUserId()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var decodedToken = handler.ReadJwtToken(token);
            return Guid.Parse(decodedToken.Subject);
        }
    }
}
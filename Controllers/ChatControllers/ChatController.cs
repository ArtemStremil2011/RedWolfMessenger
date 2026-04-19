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
using System.Security.Claims;
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

        // ВСЕ ЧАТЫ (только администраторам)
        [HttpGet]
        public async Task<IActionResult> GetAllChats()
        {
            try
            {
                _logger.LogInformation("GetAllChats called");
                var currentUserId = GetCurrentUserId();
                _logger.LogDebug($"Current user ID: {currentUserId}");

                var currentUser = await _context.Users.FindAsync(currentUserId);

                if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning($"User {currentUserId} with role {currentUser.Role} attempted to access all chats");
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

        // КОНКРЕТНЫЙ ЧАТ
        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(Guid chatId)
        {
            try
            {
                _logger.LogInformation($"GetChat called with chatId: {chatId}");
                var currentUserId = GetCurrentUserId();
                _logger.LogDebug($"Current user ID: {currentUserId}");

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Where(c => c.Id == chatId)
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
                        c.CreatedAt,
                        c.LastActivityAt
                    ))
                    .FirstOrDefaultAsync();

                if (chat == null)
                {
                    _logger.LogWarning($"Chat with Id {chatId} not found");
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                var isUserInChat = await _context.Chats
                    .Where(c => c.Id == chatId)
                    .AnyAsync(c => c.Users.Any(u => u.Id == currentUserId));

                if (!isUserInChat)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to access chat {chatId} without permission");
                    return Forbid("Вы не имеете доступа к этому чату");
                }

                _logger.LogInformation($"Chat {chatId} retrieved successfully");
                return Ok(chat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetChat for chatId: {chatId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // СПИСОК ЧАТОВ ПОЛЬЗОВАТЕЛЯ
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
                    _logger.LogWarning($"User {currentUserId} attempted to view chats of user {userId}");
                    return Forbid("Вы можете просматривать только свои чаты");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User with Id {userId} not found");
                    return NotFound($"Пользователь с Id {userId} не найден");
                }

                var chats = await _context.Chats
                    .Include(c => c.Users)
                    .Include(c => c.CreatedBy)
                    .Where(c => c.Users.Any(u => u.Id == userId))
                    .Select(c => new ChatResponseDTO(
                        c.Id,
                        c.ChatName,
                        c.Users.Select(u => new UserResponseDTO(
                            u.Id,
                            u.Name,
                            u.AvatarPath,
                            u.RegisterDate
                        )).ToList(),
                        c.Users.Where(u => u.Id != userId).Select(u => new UserResponseDTO(
                            u.Id,
                            u.Name,
                            u.AvatarPath,
                            u.RegisterDate
                        )).FirstOrDefault(),
                        c.CreatedAt,
                        c.LastActivityAt
                    ))
                    .ToListAsync();

                _logger.LogInformation($"GetUserChats returned {chats.Count} chats for user {userId}");
                return Ok(chats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetUserChats for userId: {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // СООБЩЕНИЯ ЧАТА
        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetChatMessages(Guid chatId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogInformation($"GetChatMessages called for chatId: {chatId}, page: {page}, pageSize: {pageSize}");
                var currentUserId = GetCurrentUserId();

                var chat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    _logger.LogWarning($"Chat with Id {chatId} not found");
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                var isUserInChat = chat.Users.Any(u => u.Id == currentUserId);
                if (!isUserInChat)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to view messages of chat {chatId} without permission");
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
                _logger.LogInformation($"GetChatMessages returned {messages.Count} messages (total: {total}) for chat {chatId}");

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

        // СОЗДАНИЕ ЧАТА (по ID)
        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDTO createChatDto)
        {
            try
            {
                _logger.LogInformation($"CreateChat called with User1Id: {createChatDto.User1Id}, User2Id: {createChatDto.User2Id}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state in CreateChat");
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();

                if (currentUserId != createChatDto.User1Id)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to create chat as User1Id {createChatDto.User1Id}");
                    return Forbid("Вы не можете создавать чат от имени другого пользователя");
                }

                var user1 = await _context.Users.FindAsync(createChatDto.User1Id);
                if (user1 == null)
                {
                    _logger.LogWarning($"User1 with Id {createChatDto.User1Id} not found");
                    return BadRequest($"Пользователь с Id {createChatDto.User1Id} не найден");
                }

                var user2 = await _context.Users.FindAsync(createChatDto.User2Id);
                if (user2 == null)
                {
                    _logger.LogWarning($"User2 with Id {createChatDto.User2Id} not found");
                    return BadRequest($"Пользователь с Id {createChatDto.User2Id} не найден");
                }

                if (user1.Id == user2.Id)
                {
                    _logger.LogWarning($"User {user1.Id} attempted to create chat with themselves");
                    return BadRequest("Нельзя создать чат с самим собой");
                }

                var existingChat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Users.Count == 2 &&
                        c.Users.Any(u => u.Id == createChatDto.User1Id) &&
                        c.Users.Any(u => u.Id == createChatDto.User2Id));

                if (existingChat != null)
                {
                    _logger.LogInformation($"Chat already exists between {createChatDto.User1Id} and {createChatDto.User2Id}");
                    return BadRequest("Чат между этими пользователями уже существует");
                }

                var chat = new Chat
                {
                    ChatName = createChatDto.ChatName ?? $"{user1.Name} & {user2.Name}",
                    MaxUsers = 2,
                    IsPrivate = true,
                    CreatedById = createChatDto.User1Id,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                };

                chat.Users.Add(user1);
                chat.Users.Add(user2);

                await _context.Chats.AddAsync(chat);
                await _context.SaveChangesAsync();

                await _context.Entry(chat)
                    .Collection(c => c.Users)
                    .LoadAsync();

                var response = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(
                        u.Id,
                        u.Name,
                        u.AvatarPath,
                        u.RegisterDate
                    )).ToList(),
                    new UserResponseDTO(user2.Id, user2.Name, user2.AvatarPath, user2.RegisterDate),
                    chat.CreatedAt,
                    chat.LastActivityAt
                );

                _logger.LogInformation($"Chat created successfully: {chat.Id} between {user1.Name} and {user2.Name}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateChat");
                return StatusCode(500, "Internal server error");
            }
        }

        // УДАЛЕНИЕ ЧАТА
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
                    _logger.LogWarning($"Chat with Id {id} not found");
                    return NotFound($"Чат с Id {id} не найден");
                }

                var isUserInChat = chat.Users.Any(u => u.Id == currentUserId);

                if (!isUserInChat && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to delete chat {id} without permission");
                    return Forbid("Вы не имеете права удалять этот чат");
                }

                if (chat.MessagesHistory != null && chat.MessagesHistory.Any())
                {
                    _logger.LogInformation($"Deleting {chat.MessagesHistory.Count} messages from chat {id}");
                    _context.Messages.RemoveRange(chat.MessagesHistory);
                }

                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Chat {id} deleted successfully");
                return Ok(new { message = "Чат успешно удалён", id = chat.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeleteChat for chatId: {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // СОЗДАНИЕ ЧАТА ПО ИМЕНИ ПОЛЬЗОВАТЕЛЯ
        // ==========================================

        /// <summary>
        /// Создание чата с пользователем по его имени
        /// </summary>
        [HttpPost("create-by-name")]
        public async Task<IActionResult> CreateChatByUsername([FromBody] CreateChatByNameDTO createDto)
        {
            try
            {
                _logger.LogInformation($"CreateChatByUsername called with username: {createDto.Username}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state in CreateChatByUsername");
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                _logger.LogDebug($"Current user ID: {currentUserId}");

                var currentUser = await _context.Users.FindAsync(currentUserId);

                if (currentUser == null)
                {
                    _logger.LogError($"Current user with ID {currentUserId} not found");
                    return Unauthorized("Пользователь не найден");
                }

                // Ищем пользователя по имени
                _logger.LogDebug($"Searching for user with name: {createDto.Username}");
                var targetUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Name == createDto.Username);

                if (targetUser == null)
                {
                    _logger.LogWarning($"User with name '{createDto.Username}' not found");
                    return NotFound($"Пользователь с именем '{createDto.Username}' не найден");
                }

                _logger.LogDebug($"Target user found: {targetUser.Id} - {targetUser.Name}");

                if (targetUser.Id == currentUserId)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to create chat with themselves");
                    return BadRequest("Нельзя создать чат с самим собой");
                }

                // Проверяем, существует ли уже чат между этими пользователями
                _logger.LogDebug($"Checking for existing chat between {currentUserId} and {targetUser.Id}");
                var existingChat = await _context.Chats
                    .Include(c => c.Users)
                    .FirstOrDefaultAsync(c => c.Users.Count == 2 &&
                        c.Users.Any(u => u.Id == currentUserId) &&
                        c.Users.Any(u => u.Id == targetUser.Id));

                if (existingChat != null)
                {
                    _logger.LogInformation($"Existing chat found: {existingChat.Id} between {currentUserId} and {targetUser.Id}");

                    // Возвращаем существующий чат
                    var response = new ChatResponseDTO(
                        existingChat.Id,
                        existingChat.ChatName,
                        existingChat.Users.Select(u => new UserResponseDTO(
                            u.Id,
                            u.Name,
                            u.AvatarPath,
                            u.RegisterDate
                        )).ToList(),
                        new UserResponseDTO(targetUser.Id, targetUser.Name, targetUser.AvatarPath, targetUser.RegisterDate),
                        existingChat.CreatedAt,
                        existingChat.LastActivityAt
                    );

                    return Ok(response);
                }

                // Создаём новый чат
                _logger.LogInformation($"Creating new chat between {currentUser.Name} and {targetUser.Name}");
                var chat = new Chat
                {
                    ChatName = createDto.ChatName ?? $"{currentUser.Name} & {targetUser.Name}",
                    MaxUsers = 2,
                    IsPrivate = true,
                    CreatedById = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                };

                chat.Users.Add(currentUser);
                chat.Users.Add(targetUser);

                await _context.Chats.AddAsync(chat);
                await _context.SaveChangesAsync();

                await _context.Entry(chat)
                    .Collection(c => c.Users)
                    .LoadAsync();

                var chatResponse = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(
                        u.Id,
                        u.Name,
                        u.AvatarPath,
                        u.RegisterDate
                    )).ToList(),
                    new UserResponseDTO(targetUser.Id, targetUser.Name, targetUser.AvatarPath, targetUser.RegisterDate),
                    chat.CreatedAt,
                    chat.LastActivityAt
                );

                _logger.LogInformation($"Chat created successfully: {chat.Id} between {currentUser.Name} (ID: {currentUserId}) and {targetUser.Name} (ID: {targetUser.Id})");
                return Ok(chatResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in CreateChatByUsername for username: {createDto?.Username ?? "null"}");
                return StatusCode(500, "Internal server error");
            }
        }

        // ВНУТРЕННИЕ МЕТОДЫ

        // Получение ID текущего пользователя из токена
        private Guid GetCurrentUserId()
        {
            try
            {
                var authorizationHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authorizationHeader))
                {
                    _logger.LogWarning("Authorization header is missing");
                    throw new UnauthorizedAccessException("Authorization header is missing");
                }

                var token = authorizationHeader.Replace("Bearer ", "");
                _logger.LogDebug("Extracting user ID from token");

                var handler = new JwtSecurityTokenHandler();
                var decodedToken = handler.ReadJwtToken(token);
                var userIdClaim = decodedToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                {
                    _logger.LogError("No user ID claim found in token");
                    throw new UnauthorizedAccessException("No user ID claim found");
                }

                var userId = Guid.Parse(userIdClaim.Value);
                _logger.LogDebug($"User ID extracted: {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user ID from token");
                throw;
            }
        }
    }
}
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
                    return Forbid("Только администраторы могут просматривать все чаты");
                }

                var chats = await _context.Chats
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

                return Ok(chats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllChats");
                return StatusCode(500, "Internal server error");
            }
        }

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
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                if (!chat.Users.Any(u => u.Id == currentUserId))
                {
                    return Forbid("Вы не имеете доступа к этому чату");
                }

                var otherUser = chat.MaxUsers == 2 && chat.Users.Count == 2
                    ? chat.Users.FirstOrDefault(u => u.Id != currentUserId)
                    : null;

                var response = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                    otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
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
                    return Forbid("Вы можете просматривать только свои чаты");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
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
                        chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                        otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
                        chat.MaxUsers,
                        chat.CreatedAt,
                        chat.LastActivityAt
                    );
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetUserChats for userId: {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

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
                    return NotFound($"Чат с Id {chatId} не найден");
                }

                if (!chat.Users.Any(u => u.Id == currentUserId))
                {
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
                        m.MessageCreator != null ? new UserResponseDTO(m.MessageCreator.Id, m.MessageCreator.Name, m.MessageCreator.AvatarPath, m.MessageCreator.RegisterDate) : null,
                        m.IsDeleted
                    ))
                    .ToListAsync();

                var total = await _context.Messages.CountAsync(m => m.ChatId == chatId && !m.IsDeleted);

                return Ok(new { page, pageSize, total, messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetChatMessages for chatId: {chatId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDTO createChatDto)
        {
            try
            {
                _logger.LogInformation($"CreateChat called with {createChatDto.MemberIds?.Count ?? 0} members");

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();

                if (!createChatDto.MemberIds.Contains(currentUserId))
                {
                    return BadRequest("Вы должны быть в списке участников");
                }

                var memberIds = createChatDto.MemberIds.Distinct().ToList();

                if (memberIds.Count < 2)
                {
                    return BadRequest("В чате должно быть минимум 2 участника");
                }

                var maxUsers = createChatDto.MaxUsers ?? memberIds.Count;
                if (memberIds.Count > maxUsers)
                {
                    return BadRequest($"Количество участников ({memberIds.Count}) превышает MaxUsers ({maxUsers})");
                }

                var users = new List<User>();
                foreach (var userId in memberIds)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        return BadRequest($"Пользователь {userId} не найден");
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
                        return Ok(new ChatResponseDTO(
                            existingChat.Id,
                            existingChat.ChatName,
                            existingChat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                            otherUser != null ? new UserResponseDTO(otherUser.Id, otherUser.Name, otherUser.AvatarPath, otherUser.RegisterDate) : null,
                            existingChat.MaxUsers,
                            existingChat.CreatedAt,
                            existingChat.LastActivityAt
                        ));
                    }
                }

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

                var response = new ChatResponseDTO(
                    chat.Id,
                    chat.ChatName,
                    chat.Users.Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate)).ToList(),
                    responseOtherUser != null ? new UserResponseDTO(responseOtherUser.Id, responseOtherUser.Name, responseOtherUser.AvatarPath, responseOtherUser.RegisterDate) : null,
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

        [HttpPut("{chatId}")]
        public async Task<IActionResult> UpdateChatName(Guid chatId, [FromBody] UpdateChatNameDTO dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    return NotFound("Чат не найден");
                }

                if (chat.CreatedById != currentUserId)
                {
                    return Forbid("Только создатель может изменить название");
                }

                chat.ChatName = dto.ChatName;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Название обновлено", chatName = chat.ChatName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UpdateChatName for chatId: {chatId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("add-user")]
        public async Task<IActionResult> AddUserToChat([FromBody] AddUserToChatDTO dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var chat = await _context.Chats.Include(c => c.Users).FirstOrDefaultAsync(c => c.Id == dto.ChatId);

                if (chat == null) return NotFound("Чат не найден");
                if (chat.CreatedById != currentUserId) return Forbid("Только создатель может добавлять участников");

                var userToAdd = await _context.Users.FindAsync(dto.UserId);
                if (userToAdd == null) return NotFound("Пользователь не найден");
                if (chat.Users.Any(u => u.Id == dto.UserId)) return BadRequest("Пользователь уже в чате");
                if (chat.Users.Count >= chat.MaxUsers) return BadRequest($"Достигнут лимит участников ({chat.MaxUsers})");

                chat.Users.Add(userToAdd);
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

                return Ok(new { message = "Пользователь добавлен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddUserToChat");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("remove-user")]
        public async Task<IActionResult> RemoveUserFromChat([FromBody] AddUserToChatDTO dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var chat = await _context.Chats.Include(c => c.Users).FirstOrDefaultAsync(c => c.Id == dto.ChatId);

                if (chat == null) return NotFound("Чат не найден");

                var userToRemove = await _context.Users.FindAsync(dto.UserId);
                if (userToRemove == null) return NotFound("Пользователь не найден");
                if (!chat.Users.Any(u => u.Id == dto.UserId)) return BadRequest("Пользователь не в чате");

                bool isCreator = chat.CreatedById == currentUserId;
                bool isSelf = currentUserId == dto.UserId;

                if (!isCreator && !isSelf) return Forbid("Вы не можете удалить этого пользователя");
                if (chat.CreatedById == dto.UserId && !isSelf) return BadRequest("Нельзя удалить создателя группы");

                chat.Users.Remove(userToRemove);
                await _context.SaveChangesAsync();

                var currentUser = await _context.Users.FindAsync(currentUserId);
                var systemMessage = new Message
                {
                    MessageText = isSelf ? $"{userToRemove.Name} left the group" : $"{currentUser?.Name} removed {userToRemove.Name} from group",
                    UserId = currentUserId,
                    ChatId = chat.Id,
                    IsSystemMessage = true,
                    MessageCreateDate = DateTime.UtcNow,
                    MessageLastUpdateDate = DateTime.UtcNow
                };
                await _context.Messages.AddAsync(systemMessage);
                await _context.SaveChangesAsync();

                return Ok(new { message = isSelf ? "Вы вышли из чата" : "Пользователь удалён", chatId = chat.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveUserFromChat");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var chat = await _context.Chats.Include(c => c.Users).FirstOrDefaultAsync(c => c.Id == id);

                if (chat == null) return NotFound("Чат не найден");

                if (chat.CreatedById != currentUserId)
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId);
                    if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                        return Forbid("Вы не имеете права удалять этот чат");
                }

                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Чат успешно удалён" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in DeleteChat for chatId: {id}");
                return StatusCode(500, "Internal server error");
            }
        }

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
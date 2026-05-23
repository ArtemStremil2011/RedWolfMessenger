using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Messenger.Data;
using Microsoft.EntityFrameworkCore;
using Messenger.Models.ChatModels;
using Messenger.Models.BaseModels;

namespace Messenger.Controllers.ChatControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatReadService _chatReadService;
        private readonly IChatWriteService _chatWriteService;
        private readonly ILogger<ChatController> _logger;
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _environment;

        public ChatController(
            IChatReadService chatReadService,
            IChatWriteService chatWriteService,
            ILogger<ChatController> logger,
            AppDBContext context,
            IWebHostEnvironment environment)
        {
            _chatReadService = chatReadService;
            _chatWriteService = chatWriteService;
            _logger = logger;
            _context = context;
            _environment = environment;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID not found in token");

            return Guid.Parse(userIdClaim);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllChats()
        {
            var currentUserId = GetCurrentUserId();
            var chats = await _chatReadService.GetAllChatsAsync(currentUserId);
            return Ok(chats);
        }

        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(Guid chatId)
        {
            var currentUserId = GetCurrentUserId();
            var chat = await _chatReadService.GetChatAsync(chatId, currentUserId);

            if (chat == null)
                return NotFound(new { message = $"Чат с Id {chatId} не найден" });

            return Ok(chat);
        }

        [HttpGet("user-chats/{userId}")]
        public async Task<IActionResult> GetUserChats(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var chats = await _chatReadService.GetUserChatsAsync(userId, currentUserId);
            return Ok(chats);
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetChatMessages(Guid chatId, int page = 1, int pageSize = 50)
        {
            var currentUserId = GetCurrentUserId();

            var messages = await _chatReadService.GetChatMessagesAsync(chatId, currentUserId, page, pageSize);
            var total = await _chatReadService.GetTotalMessagesCountAsync(chatId);

            return Ok(new { page, pageSize, total, messages });
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDTO createChatDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var chat = await _chatWriteService.CreateChatAsync(createChatDto, currentUserId);

            if (chat == null)
                return BadRequest(new { message = "Не удалось создать чат" });

            return Ok(chat);
        }

        [HttpPut("{chatId}")]
        public async Task<IActionResult> UpdateChatName(Guid chatId, [FromBody] UpdateChatNameDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.UpdateChatNameAsync(chatId, dto.ChatName, currentUserId);

            if (!result)
                return NotFound(new { message = "Чат не найден или нет прав" });

            return Ok(new { message = "Название обновлено" });
        }

        [HttpPost("add-user")]
        public async Task<IActionResult> AddUserToChat([FromBody] AddUserToChatDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.AddUserToChatAsync(dto.ChatId, dto.UserId, currentUserId);

            if (!result)
                return BadRequest(new { message = "Не удалось добавить пользователя" });

            return Ok(new { message = "Пользователь добавлен" });
        }

        [HttpPost("remove-user")]
        public async Task<IActionResult> RemoveUserFromChat([FromBody] AddUserToChatDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.RemoveUserFromChatAsync(dto.ChatId, dto.UserId, currentUserId);

            if (!result)
                return BadRequest(new { message = "Не удалось удалить пользователя" });

            return Ok(new { message = "Пользователь удалён" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.DeleteChatAsync(id, currentUserId);

            if (!result)
                return NotFound(new { message = "Чат не найден или нет прав" });

            return Ok(new { message = "Чат успешно удалён", id });
        }

        [HttpPost("{chatId}/leave")]
        public async Task<IActionResult> LeaveGroup(Guid chatId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.LeaveGroupAsync(chatId, currentUserId);

            if (!result)
                return BadRequest(new { message = "Не удалось выйти из чата" });

            return Ok(new { message = "Вы вышли из чата" });
        }

        [HttpGet("user-status/{userId}")]
        public async Task<IActionResult> GetUserStatus(Guid userId)
        {
            var status = await _chatReadService.GetUserStatusAsync(userId);
            return Ok(status);
        }

        [HttpGet("user-profile/{userId}")]
        public async Task<IActionResult> GetUserProfile(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _chatReadService.GetUserProfileForChatAsync(userId, currentUserId);

            if (user == null)
                return NotFound(new { message = "Пользователь не найден или нет доступа" });

            return Ok(user);
        }

        // ============ АВАТАРКИ ДЛЯ ГРУПП ============

        [HttpPost("{chatId}/avatar")]
        public async Task<IActionResult> UploadGroupAvatar(Guid chatId, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Файл не выбран" });

                var currentUserId = GetCurrentUserId();
                var avatarPath = await _chatWriteService.UploadGroupAvatarAsync(chatId, file, currentUserId);

                if (avatarPath == null)
                    return BadRequest(new { message = "Не удалось загрузить аватарку" });

                return Ok(new { avatarPath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading group avatar");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
            }
        }

        [HttpGet("{chatId}/avatar")]
        public async Task<IActionResult> GetGroupAvatar(Guid chatId)
        {
            var currentUserId = GetCurrentUserId();
            var avatarPath = await _chatReadService.GetGroupAvatarPathAsync(chatId, currentUserId);

            if (string.IsNullOrEmpty(avatarPath))
                return NotFound();

            var filePath = Path.Combine(_environment.WebRootPath, avatarPath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(Path.GetExtension(filePath));
            return File(bytes, contentType);
        }

        [HttpDelete("{chatId}/avatar")]
        public async Task<IActionResult> DeleteGroupAvatar(Guid chatId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.DeleteGroupAvatarAsync(chatId, currentUserId);

            if (!result)
                return BadRequest(new { message = "Не удалось удалить аватарку" });

            return Ok(new { message = "Аватарка удалена" });
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
        }
    }
}
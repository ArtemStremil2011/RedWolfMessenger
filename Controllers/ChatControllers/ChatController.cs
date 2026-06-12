using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Messenger.Hubs;
using System.Security.Cryptography;
using System.Text;
using Messenger.Data;

namespace Messenger.Controllers.ChatControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatReadService _chatReadService;
        private readonly IChatWriteService _chatWriteService;
        private readonly IUserReadService _userReadService;
        private readonly ILogger<ChatController> _logger;
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<MessengerHub> _hubContext;

        public ChatController(
            IChatReadService chatReadService,
            IChatWriteService chatWriteService,
            IUserReadService userReadService,
            ILogger<ChatController> logger,
            AppDBContext context,
            IWebHostEnvironment environment,
            IHubContext<MessengerHub> hubContext)
        {
            _chatReadService = chatReadService;
            _chatWriteService = chatWriteService;
            _userReadService = userReadService;
            _logger = logger;
            _context = context;
            _environment = environment;
            _hubContext = hubContext;
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
            try
            {
                Console.WriteLine("=== CREATE CHAT START ===");
                Console.WriteLine($"MemberIds: {string.Join(", ", createChatDto.MemberIds)}");
                
                if (!ModelState.IsValid)
                {
                    Console.WriteLine("ModelState invalid");
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                Console.WriteLine($"CurrentUserId: {currentUserId}");

                // Проверяем, что все участники существуют
                foreach (var memberId in createChatDto.MemberIds)
                {
                    var user = await _userReadService.GetProfileAsync(memberId);
                    if (user == null)
                    {
                        Console.WriteLine($"User {memberId} not found");
                        return BadRequest(new { message = $"Пользователь {memberId} не найден" });
                    }
                    
                    // Проверяем, есть ли публичный ключ
                    if (string.IsNullOrEmpty(user.PublicKey))
                    {
                        Console.WriteLine($"User {memberId} has no public key");
                        return BadRequest(new { message = $"У пользователя {user.Name} нет публичного ключа. Попробуйте перерегистрироваться." });
                    }
                }

                var chat = await _chatWriteService.CreateChatAsync(createChatDto, currentUserId);
                
                if (chat == null)
                {
                    Console.WriteLine("Chat creation returned null");
                    return BadRequest(new { message = "Не удалось создать чат" });
                }

                Console.WriteLine($"Chat created: {chat.Id}");
                
                // Генерируем сессионный ключ
                var sessionKey = GenerateSessionKey();
                var sessionKeyBase64 = Convert.ToBase64String(sessionKey);
                Console.WriteLine($"Session key generated: {sessionKeyBase64.Substring(0, 20)}...");
                
                var encryptedKeys = new Dictionary<Guid, string>();
                
                foreach (var user in chat.Users)
                {
                    var userProfile = await _userReadService.GetProfileAsync(user.Id);
                    if (string.IsNullOrEmpty(userProfile?.PublicKey))
                    {
                        Console.WriteLine($"User {user.Id} has no public key");
                        continue;
                    }
                    
                    var encryptedKey = EncryptWithPublicKey(sessionKey, userProfile.PublicKey);
                    encryptedKeys[user.Id] = encryptedKey;
                    Console.WriteLine($"Encrypted key for user {user.Id}");
                }
                
                // Сохраняем ключи
                await _chatWriteService.SaveSessionKeysAsync(chat.Id, encryptedKeys, currentUserId);
                Console.WriteLine($"Session keys saved for chat {chat.Id}");
                
                // Отправляем уведомления
                foreach (var user in chat.Users)
                {
                    await _hubContext.Clients.User(user.Id.ToString()).SendAsync("NewChatCreated", chat);
                }
                
                return Ok(chat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION: {ex.Message}");
                Console.WriteLine($"STACK: {ex.StackTrace}");
                _logger.LogError(ex, "Error creating chat");
                return StatusCode(500, new { message = ex.Message });
            }
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

        [HttpGet("{chatId}/session-key")]
        public async Task<IActionResult> GetSessionKey(Guid chatId)
        {
            var userId = GetCurrentUserId();
            var encryptedKey = await _chatReadService.GetSessionKeyAsync(chatId, userId);
            
            if (string.IsNullOrEmpty(encryptedKey))
                return NotFound(new { message = "Сессионный ключ не найден" });
            
            return Ok(new { encryptedKey });
        }

        [HttpPost("{chatId}/session-keys")]
        public async Task<IActionResult> SaveSessionKeys(Guid chatId, [FromBody] CreateSessionKeyDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _chatWriteService.SaveSessionKeysAsync(chatId, dto.EncryptedKeys, currentUserId);
            
            if (!result)
                return BadRequest(new { message = "Не удалось сохранить сессионные ключи" });
            
            return Ok(new { message = "Сессионные ключи сохранены" });
        }

        private byte[] GenerateSessionKey()
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                return aes.Key;
            }
        }

        private string EncryptWithPublicKey(byte[] data, string publicKeyBase64)
        {
            try
            {
                // Очищаем ключ от лишних символов
                var cleanKey = publicKeyBase64?.Trim();
                cleanKey = cleanKey?.Replace("\n", "")?.Replace("\r", "")?.Replace(" ", "");
                
                if (string.IsNullOrEmpty(cleanKey))
                    throw new Exception("Public key is empty");
                
                var publicKeyBytes = Convert.FromBase64String(cleanKey);
                
                using (var rsa = RSA.Create())
                {
                    // Пробуем разные форматы импорта
                    try
                    {
                        // Формат SubjectPublicKeyInfo (SPKI) — стандартный
                        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                    }
                    catch
                    {
                        try
                        {
                            // Формат PKCS#1 RSAPublicKey
                            rsa.ImportRSAPublicKey(publicKeyBytes, out _);
                        }
                        catch
                        {
                            // Если ничего не работает — показываем ошибку
                            throw new Exception("Cannot import public key. Invalid format.");
                        }
                    }
                    
                    var encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    return Convert.ToBase64String(encrypted);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRYPTO ERROR] EncryptWithPublicKey: {ex.Message}");
                throw;
            }
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
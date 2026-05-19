using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Messenger.Controllers.BaseControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserReadService _userReadService;
        private readonly IUserWriteService _userWriteService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserReadService userReadService,
            IUserWriteService userWriteService,
            ILogger<UserController> logger)
        {
            _userReadService = userReadService;
            _userWriteService = userWriteService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID not found in token");

            return Guid.Parse(userIdClaim);
        }

        [AllowAnonymous]
        [HttpPost("request-verification")]
        public async Task<IActionResult> RequestVerification([FromBody] UserRegisterDTO registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var code = await _userWriteService.RequestVerificationCodeAsync(registerDto);

            if (code == null)
                return Conflict(new { message = "Пользователь с таким номером или именем уже существует" });

            return Ok(new { message = "Код подтверждения отправлен", code, phoneNumber = registerDto.PhoneNumber });
        }

        [AllowAnonymous]
        [HttpPost("verify-and-register")]
        public async Task<IActionResult> VerifyAndRegister([FromBody] VerifyPhoneDTO verifyDto)  // ← VerifyPhoneDTO
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (success, token, userId) = await _userWriteService.VerifyAndRegisterAsync(
                verifyDto.PhoneNumber,
                verifyDto.Code);  // ← Code, а не VerifyDto.Code

            if (!success)
                return BadRequest(new { message = "Неверный код или время истекло" });

            return Ok(new { message = "Регистрация успешна", userId, token });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (success, token, userId) = await _userWriteService.LoginAsync(loginDto);

            if (!success)
                return Unauthorized(new { message = "Неверный логин или пароль" });

            return Ok(new { message = "Вход выполнен успешно", userId, token });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            var profile = await _userReadService.GetProfileAsync(userId);

            if (profile == null)
                return NotFound(new { message = "Пользователь не найден" });

            return Ok(profile);
        }

        [Authorize]
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userReadService.GetUserByIdAsync(userId, currentUserId);

            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });

            return Ok(user);
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userReadService.GetAllUsersAsync();
            return Ok(users);
        }

        [Authorize]
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateDTO updateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _userWriteService.UpdateProfileAsync(userId, updateDto);

            if (!result)
                return NotFound(new { message = "Пользователь не найден" });

            return Ok(new { message = "Профиль успешно обновлён" });
        }

        [Authorize]
        [HttpPost("upload-avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Файл не выбран" });

            var userId = GetCurrentUserId();
            var result = await _userWriteService.UploadAvatarAsync(userId, file);

            if (!result)
                return BadRequest(new { message = "Не удалось загрузить аватарку" });

            return Ok(new { message = "Аватарка успешно загружена" });
        }

        [Authorize]
        [HttpDelete("avatar")]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userId = GetCurrentUserId();
            var result = await _userWriteService.DeleteAvatarAsync(userId);

            if (!result)
                return NotFound(new { message = "Пользователь не найден" });

            return Ok(new { message = "Аватарка удалена" });
        }

        [AllowAnonymous]
        [HttpGet("avatar/{userId}")]
        public async Task<IActionResult> GetAvatar(Guid userId)
        {
            // Этот метод лучше оставить отдельно, так как он возвращает файл
            // Можно перенести в отдельный сервис, но пока оставим
            var user = await _userReadService.GetProfileAsync(userId);

            if (user == null || string.IsNullOrEmpty(user.AvatarPath))
            {
                var defaultAvatarPath = Path.Combine("wwwroot", "avatars", "default-avatar.png");
                if (System.IO.File.Exists(defaultAvatarPath))
                {
                    var imageBytes = await System.IO.File.ReadAllBytesAsync(defaultAvatarPath);
                    return File(imageBytes, "image/png");
                }
                return NotFound();
            }

            var avatarPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
            if (!System.IO.File.Exists(avatarPath))
            {
                var defaultAvatarPath = Path.Combine("wwwroot", "avatars", "default-avatar.png");
                if (System.IO.File.Exists(defaultAvatarPath))
                {
                    var imageBytes = await System.IO.File.ReadAllBytesAsync(defaultAvatarPath);
                    return File(imageBytes, "image/png");
                }
                return NotFound();
            }

            var imageBytesResult = await System.IO.File.ReadAllBytesAsync(avatarPath);
            var contentType = GetContentType(Path.GetExtension(avatarPath));
            return File(imageBytesResult, contentType);
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

        [Authorize]
        [HttpDelete("delete-user/{userId}")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _userWriteService.DeleteUserAsync(userId, currentUserId);

            if (!result)
                return NotFound(new { message = "Пользователь не найден или недостаточно прав" });

            return Ok(new { message = "Пользователь успешно удалён", id = userId });
        }
    }
}
using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Messenger.Controllers.BaseControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDBContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;

        // Временное хранилище для кодов подтверждения
        private static readonly Dictionary<string, TempRegistration> _tempRegistrations = new();

        public UserController(AppDBContext context, IConfiguration configuration, ILogger<UserController> logger)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _configuration = configuration;
            _logger = logger;
        }

        public class TempRegistration
        {
            public string PhoneNumber { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string PasswordHash { get; set; } = string.Empty;
            public string VerificationCode { get; set; } = string.Empty;
            public DateTime ExpiryTime { get; set; }
        }

        // ==========================================
        // ШАГ 1: ЗАПРОС КОДА ПОДТВЕРЖДЕНИЯ
        // ==========================================

        [AllowAnonymous]
        [HttpPost("request-verification")]
        public async Task<IActionResult> RequestVerification([FromBody] UserRegisterDTO registerDto)
        {
            _logger.LogDebug("Запрос кода подтверждения для {PhoneNumber}", registerDto.PhoneNumber);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == registerDto.PhoneNumber || u.Name == registerDto.Name);

            if (existingUser != null)
            {
                return Conflict("Пользователь с таким номером телефона или именем уже существует");
            }

            var code = new Random().Next(100000, 999999).ToString();
            var hashedPassword = _passwordHasher.HashPassword(null, registerDto.Password);

            var tempReg = new TempRegistration
            {
                PhoneNumber = registerDto.PhoneNumber,
                Name = registerDto.Name,
                PasswordHash = hashedPassword,
                VerificationCode = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(5)
            };

            _tempRegistrations[registerDto.PhoneNumber] = tempReg;

            _logger.LogInformation($"Код для {registerDto.PhoneNumber}: {code}");

            return Ok(new
            {
                message = "Код подтверждения отправлен",
                code = code,
                phoneNumber = registerDto.PhoneNumber
            });
        }

        // ==========================================
        // ШАГ 2: ПОДТВЕРЖДЕНИЕ И РЕГИСТРАЦИЯ
        // ==========================================

        [AllowAnonymous]
        [HttpPost("verify-and-register")]
        public async Task<IActionResult> VerifyAndRegister([FromBody] VerifyCodeDTO verifyDto)
        {
            _logger.LogDebug("Подтверждение кода для {PhoneNumber}", verifyDto.PhoneNumber);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_tempRegistrations.TryGetValue(verifyDto.PhoneNumber, out var tempReg))
            {
                return BadRequest("Код не запрошен или время истекло. Начните регистрацию заново.");
            }

            if (tempReg.ExpiryTime < DateTime.UtcNow)
            {
                _tempRegistrations.Remove(verifyDto.PhoneNumber);
                return BadRequest("Время действия кода истекло. Запросите новый код.");
            }

            if (tempReg.VerificationCode != verifyDto.Code)
            {
                return BadRequest("Неверный код подтверждения");
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == tempReg.PhoneNumber || u.Name == tempReg.Name);

            if (existingUser != null)
            {
                _tempRegistrations.Remove(verifyDto.PhoneNumber);
                return Conflict("Пользователь уже существует");
            }

            var user = new User
            {
                PhoneNumber = tempReg.PhoneNumber,
                Name = tempReg.Name,
                PasswordHash = tempReg.PasswordHash,
                IsPhoneNumberConfirmed = true,
                RegisterDate = DateTime.UtcNow,
                AvatarPath = "/avatars/default-avatar.png"
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            _tempRegistrations.Remove(verifyDto.PhoneNumber);

            _logger.LogInformation("Пользователь зарегистрирован: {UserId}", user.Id);

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                message = "Регистрация успешна",
                userId = user.Id,
                token = token
            });
        }

        // ==========================================
        // ВХОД
        // ==========================================

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO loginDto)
        {
            _logger.LogDebug("Попытка входа: {Login}", loginDto.Login);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == loginDto.Login || u.Name == loginDto.Login);

            if (user == null)
            {
                return Unauthorized("Неверный логин или пароль");
            }

            if (!user.IsPhoneNumberConfirmed)
            {
                return Unauthorized("Подтвердите номер телефона перед входом");
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Неверный логин или пароль");
            }

            var token = GenerateJwtToken(user);

            _logger.LogInformation("Пользователь вошёл: {UserId}", user.Id);

            return Ok(new
            {
                message = "Вход выполнен успешно",
                userId = user.Id,
                token = token
            });
        }

        // ==========================================
        // ПРОФИЛЬ
        // ==========================================

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("Пользователь не найден");

            return Ok(new UserResponseDTO(
                user.Id,
                user.Name,
                user.AvatarPath,
                user.RegisterDate
            ));
        }

        [Authorize]
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUserId != userId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("Недостаточно прав");

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("Пользователь не найден");

            return Ok(new UserResponseDTO(
                user.Id,
                user.Name,
                user.AvatarPath,
                user.RegisterDate
            ));
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new UserResponseDTO(
                    u.Id,
                    u.Name,
                    u.AvatarPath,
                    u.RegisterDate
                ))
                .ToListAsync();

            return Ok(users);
        }

        // ==========================================
        // ОБНОВЛЕНИЕ ПРОФИЛЯ
        // ==========================================

        [Authorize]
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateDTO updateDto)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound("Пользователь не найден");

            if (!string.IsNullOrEmpty(updateDto.Name))
                user.Name = updateDto.Name;

            if (!string.IsNullOrEmpty(updateDto.AvatarPath))
                user.AvatarPath = updateDto.AvatarPath;

            if (!string.IsNullOrEmpty(updateDto.NewPassword))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, updateDto.NewPassword);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserResponseDTO(
                user.Id,
                user.Name,
                user.AvatarPath,
                user.RegisterDate
            ));
        }

        // ==========================================
        // АВАТАРКИ
        // ==========================================

        [Authorize]
        [HttpPost("upload-avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Файл не выбран");

                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest("Файл слишком большой. Максимум 5MB");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                    return BadRequest("Неподдерживаемый формат");

                var userId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return NotFound("Пользователь не найден");

                if (!string.IsNullOrEmpty(user.AvatarPath) && !user.AvatarPath.Contains("default-avatar"))
                {
                    var oldAvatarPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldAvatarPath))
                        System.IO.File.Delete(oldAvatarPath);
                }

                var fileName = $"{userId}_{DateTime.Now.Ticks}{extension}";
                var uploadPath = Path.Combine("wwwroot", "avatars");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                user.AvatarPath = $"/avatars/{fileName}";
                await _context.SaveChangesAsync();

                return Ok(new { avatarPath = user.AvatarPath, message = "Аватарка загружена" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        [HttpDelete("avatar")]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound("Пользователь не найден");

            if (!string.IsNullOrEmpty(user.AvatarPath) && !user.AvatarPath.Contains("default-avatar"))
            {
                var avatarPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
                if (System.IO.File.Exists(avatarPath))
                    System.IO.File.Delete(avatarPath);
            }

            user.AvatarPath = "/avatars/default-avatar.png";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Аватарка удалена", avatarPath = user.AvatarPath });
        }

        [AllowAnonymous]
        [HttpGet("avatar/{userId}")]
        public async Task<IActionResult> GetAvatar(Guid userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);

                string avatarPath;
                if (user == null || string.IsNullOrEmpty(user.AvatarPath))
                {
                    avatarPath = Path.Combine("wwwroot", "avatars", "default-avatar.png");
                }
                else
                {
                    avatarPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
                }

                if (!System.IO.File.Exists(avatarPath))
                {
                    avatarPath = Path.Combine("wwwroot", "avatars", "default-avatar.png");
                }

                var imageBytes = await System.IO.File.ReadAllBytesAsync(avatarPath);
                var contentType = GetContentType(Path.GetExtension(avatarPath));

                return File(imageBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting avatar");
                return StatusCode(500, "Internal server error");
            }
        }

        // ==========================================
        // УДАЛЕНИЕ ПОЛЬЗОВАТЕЛЯ
        // ==========================================

        [Authorize]
        [HttpDelete("delete-user/{userId}")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var userToDelete = await _context.Users.FindAsync(userId);

            if (userToDelete == null)
                return NotFound("Пользователь не найден");

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUserId != userId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("Недостаточно прав");

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пользователь удалён", id = userId });
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ==========================================

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

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.Now.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private Guid GetCurrentUserId()
        {
            var authorizationHeader = Request.Headers["Authorization"].ToString();
            var token = authorizationHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var decodedToken = handler.ReadJwtToken(token);
            return Guid.Parse(decodedToken.Subject);
        }
    }

    // DTO для подтверждения
    public class VerifyCodeDTO
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }
}
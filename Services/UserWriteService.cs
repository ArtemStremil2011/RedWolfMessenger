using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Messenger.Services
{
    public class UserWriteService : IUserWriteService
    {
        private readonly AppDBContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserWriteService> _logger;
        private static readonly Dictionary<string, TempRegistration> _tempRegistrations = new();

        private class TempRegistration
        {
            public string PhoneNumber { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string PasswordHash { get; set; } = string.Empty;
            public string VerificationCode { get; set; } = string.Empty;
            public DateTime ExpiryTime { get; set; }
        }

        public UserWriteService(AppDBContext context, IConfiguration configuration, ILogger<UserWriteService> logger)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool success, string token, Guid userId)> LoginAsync(UserLoginDTO loginDto)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == loginDto.Login || u.Name == loginDto.Login);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: user not found");
                    return (false, string.Empty, Guid.Empty);
                }

                if (!user.IsPhoneNumberConfirmed)
                {
                    _logger.LogWarning("Login failed: phone not confirmed for user {UserId}", user.Id);
                    return (false, string.Empty, Guid.Empty);
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);
                if (result == PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Login failed: wrong password for user {UserId}", user.Id);
                    return (false, string.Empty, Guid.Empty);
                }

                var token = GenerateJwtToken(user);
                return (true, token, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return (false, string.Empty, Guid.Empty);
            }
        }

        public async Task<(bool success, string token, Guid userId)> RegisterAsync(UserRegisterDTO registerDto)
        {
            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == registerDto.PhoneNumber || u.Name == registerDto.Name);

                if (existingUser != null)
                {
                    _logger.LogWarning("Registration failed: user already exists");
                    return (false, string.Empty, Guid.Empty);
                }

                var hashedPassword = _passwordHasher.HashPassword(null, registerDto.Password);

                var user = new User
                {
                    PhoneNumber = registerDto.PhoneNumber,
                    Name = registerDto.Name,
                    PasswordHash = hashedPassword,
                    IsPhoneNumberConfirmed = false,
                    RegisterDate = DateTime.UtcNow,
                    AvatarPath = "/avatars/default-avatar.png"
                };

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User registered: {UserId}", user.Id);
                return (true, string.Empty, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return (false, string.Empty, Guid.Empty);
            }
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UserUpdateDTO updateDto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                if (!string.IsNullOrEmpty(updateDto.Name))
                    user.Name = updateDto.Name;

                if (!string.IsNullOrEmpty(updateDto.AvatarPath))
                    user.AvatarPath = updateDto.AvatarPath;

                if (!string.IsNullOrEmpty(updateDto.NewPassword))
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, updateDto.NewPassword);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Profile updated for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UploadAvatarAsync(Guid userId, IFormFile file)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension for user {UserId}", userId);
                    return false;
                }

                if (file.Length > 5 * 1024 * 1024)
                {
                    _logger.LogWarning("File too large for user {UserId}", userId);
                    return false;
                }

                var fileName = $"{userId}_{DateTime.Now.Ticks}{extension}";
                var uploadPath = Path.Combine("wwwroot", "avatars");
                var filePath = Path.Combine(uploadPath, fileName);
                var relativePath = $"/avatars/{fileName}";

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                // Удаляем старую аватарку
                if (!string.IsNullOrEmpty(user.AvatarPath) && !user.AvatarPath.Contains("default-avatar"))
                {
                    var oldPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                user.AvatarPath = relativePath;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Avatar uploaded for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> DeleteAvatarAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                if (!string.IsNullOrEmpty(user.AvatarPath) && !user.AvatarPath.Contains("default-avatar"))
                {
                    var oldPath = Path.Combine("wwwroot", user.AvatarPath.TrimStart('/'));
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                user.AvatarPath = "/avatars/default-avatar.png";
                await _context.SaveChangesAsync();
                _logger.LogInformation("Avatar deleted for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting avatar for user {UserId}", userId);
                return false;
            }
        }

        public async Task<string?> RequestVerificationCodeAsync(UserRegisterDTO registerDto)
        {
            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == registerDto.PhoneNumber || u.Name == registerDto.Name);

                if (existingUser != null) return null;

                var code = new Random().Next(100000, 999999).ToString();
                var hashedPassword = _passwordHasher.HashPassword(null, registerDto.Password);

                _tempRegistrations[registerDto.PhoneNumber] = new TempRegistration
                {
                    PhoneNumber = registerDto.PhoneNumber,
                    Name = registerDto.Name,
                    PasswordHash = hashedPassword,
                    VerificationCode = code,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(5)
                };

                _logger.LogInformation("Verification code requested for {PhoneNumber}", registerDto.PhoneNumber);
                return code;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting verification code");
                return null;
            }
        }

        public async Task<(bool success, string token, Guid userId)> VerifyAndRegisterAsync(string phoneNumber, string code)
        {
            try
            {
                if (!_tempRegistrations.TryGetValue(phoneNumber, out var tempReg))
                {
                    _logger.LogWarning("Verification failed: no temp registration for {PhoneNumber}", phoneNumber);
                    return (false, string.Empty, Guid.Empty);
                }

                if (tempReg.ExpiryTime < DateTime.UtcNow)
                {
                    _tempRegistrations.Remove(phoneNumber);
                    _logger.LogWarning("Verification failed: code expired for {PhoneNumber}", phoneNumber);
                    return (false, string.Empty, Guid.Empty);
                }

                if (tempReg.VerificationCode != code)
                {
                    _logger.LogWarning("Verification failed: wrong code for {PhoneNumber}", phoneNumber);
                    return (false, string.Empty, Guid.Empty);
                }

                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == tempReg.PhoneNumber || u.Name == tempReg.Name);

                if (existingUser != null)
                {
                    _tempRegistrations.Remove(phoneNumber);
                    return (false, string.Empty, Guid.Empty);
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

                _tempRegistrations.Remove(phoneNumber);
                _logger.LogInformation("User verified and registered: {UserId}", user.Id);

                var token = GenerateJwtToken(user);
                return (true, token, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during verification and registration");
                return (false, string.Empty, Guid.Empty);
            }
        }

        public async Task<bool> DeleteUserAsync(Guid userId, Guid currentUserId)
        {
            try
            {
                var userToDelete = await _context.Users.FindAsync(userId);
                if (userToDelete == null) return false;

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUserId != userId && currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.SuperAdmin)
                {
                    _logger.LogWarning("User {CurrentUserId} tried to delete user {UserId} without permission", currentUserId, userId);
                    return false;
                }

                _context.Users.Remove(userToDelete);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User deleted: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return false;
            }
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured")));
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
    }
}
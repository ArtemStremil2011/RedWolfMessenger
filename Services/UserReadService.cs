using Messenger.Data;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Services
{
    public class UserReadService : IUserReadService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<UserReadService> _logger;

        public UserReadService(AppDBContext context, ILogger<UserReadService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserResponseDTO?> GetProfileAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return null;

                return new UserResponseDTO(user.Id, user.Name, user.AvatarPath, user.RegisterDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile for user {UserId}", userId);
                return null;
            }
        }

        public async Task<UserResponseDTO?> GetUserByIdAsync(Guid userId, Guid currentUserId)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return null;

                return new UserResponseDTO(user.Id, user.Name, user.AvatarPath, user.RegisterDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by id {UserId}", userId);
                return null;
            }
        }

        public async Task<List<UserResponseDTO>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .Select(u => new UserResponseDTO(u.Id, u.Name, u.AvatarPath, u.RegisterDate))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return new List<UserResponseDTO>();
            }
        }

        public async Task<UserResponseDTO?> GetUserByNameAsync(string name)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Name == name);

                if (user == null) return null;

                return new UserResponseDTO(user.Id, user.Name, user.AvatarPath, user.RegisterDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by name {Name}", name);
                return null;
            }
        }

        public async Task<bool> UserExistsAsync(Guid userId)
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user exists {UserId}", userId);
                return false;
            }
        }
    }
}
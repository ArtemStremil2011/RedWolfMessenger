using Messenger.Data;
using Messenger.DTOs;
using Messenger.Models.BaseModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Messenger.Controllers.BaseControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly AppDBContext _context;

        public MessageController(AppDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("Only administrators can view all messages");

            var messages = await _context.Messages
                .Include(m => m.MessageCreator)
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.MessageCreateDate)
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

            return Ok(messages);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var message = await _context.Messages
                .Include(m => m.MessageCreator)
                .Where(m => m.MessageId == id)
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
                .FirstOrDefaultAsync();

            if (message == null)
                return NotFound($"Message with Id {id} not found");

            return Ok(message);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MessageCreateDTO messageCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (currentUserId != messageCreateDto.UserId)
                return Forbid("You cannot create messages on behalf of another user");

            var user = await _context.Users.FindAsync(messageCreateDto.UserId);
            if (user == null)
                return BadRequest($"User with Id {messageCreateDto.UserId} not found");

            var chat = await _context.Chats.FindAsync(messageCreateDto.ChatId);
            if (chat == null)
                return BadRequest($"Chat with Id {messageCreateDto.ChatId} not found");

            var message = new Message
            {
                MessageText = messageCreateDto.MessageText,
                UserId = messageCreateDto.UserId,
                ChatId = messageCreateDto.ChatId,
                MessageCreateDate = DateTime.UtcNow,
                MessageLastUpdateDate = DateTime.UtcNow,
                IsDeleted = false
            };

            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();

            var response = new MessageResponseDTO(
                message.MessageId,
                message.MessageText,
                message.MessageCreateDate,
                message.MessageLastUpdateDate,
                message.UserId,
                message.ChatId,
                new UserResponseDTO(
                    user.Id,
                    user.Name,
                    user.AvatarPath,
                    user.RegisterDate
                ),
                message.IsDeleted
            );

            return CreatedAtAction(nameof(GetById), new { id = message.MessageId }, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] MessageUpdateDTO messageUpdateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (id != messageUpdateDto.MessageId)
                return BadRequest("ID mismatch");

            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            var message = await _context.Messages
                .Include(m => m.MessageCreator)
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null)
                return NotFound($"Message with Id {id} not found");

            if (message.IsDeleted)
                return BadRequest("Cannot update a deleted message");

            if (message.UserId != currentUserId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("You can only update your own messages");

            message.MessageText = messageUpdateDto.MessageText;
            message.MessageLastUpdateDate = DateTime.UtcNow;

            _context.Entry(message).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await MessageExists(id))
                    return NotFound();
                throw;
            }

            var response = new MessageResponseDTO(
                message.MessageId,
                message.MessageText,
                message.MessageCreateDate,
                message.MessageLastUpdateDate,
                message.UserId,
                message.ChatId,
                message.MessageCreator != null ? new UserResponseDTO(
                    message.MessageCreator.Id,
                    message.MessageCreator.Name,
                    message.MessageCreator.AvatarPath,
                    message.MessageCreator.RegisterDate
                ) : null,
                message.IsDeleted
            );

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null)
                return NotFound($"Message with Id {id} not found");

            if (message.IsDeleted)
                return BadRequest("Message is already deleted");

            if (message.UserId != currentUserId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("You can only delete your own messages");

            message.IsDeleted = true;
            message.MessageLastUpdateDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Message successfully deleted", id = message.MessageId });
        }

        [HttpDelete("permanent/{id}")]
        public async Task<IActionResult> PermanentDelete(Guid id)
        {
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("Only administrators can permanently delete messages");

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null)
                return NotFound($"Message with Id {id} not found");

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Message permanently deleted", id = id });
        }

        [HttpPatch("restore/{id}")]
        public async Task<IActionResult> Restore(Guid id)
        {
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null)
                return NotFound($"Message with Id {id} not found");

            if (!message.IsDeleted)
                return BadRequest("Message is not deleted");

            if (message.UserId != currentUserId && currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.SuperAdmin)
                return Forbid("You can only restore your own messages");

            message.IsDeleted = false;
            message.MessageLastUpdateDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Message successfully restored", id = message.MessageId });
        }

        private async Task<bool> MessageExists(Guid id)
        {
            return await _context.Messages.AnyAsync(m => m.MessageId == id);
        }
    }
}
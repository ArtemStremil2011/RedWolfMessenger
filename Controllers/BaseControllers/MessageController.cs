using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Messenger.Hubs;

namespace Messenger.Controllers.BaseControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly IMessageReadService _messageReadService;
        private readonly IMessageWriteService _messageWriteService;
        private readonly IChatReadService _chatReadService;
        private readonly IUserReadService _userReadService;
        private readonly ILogger<MessageController> _logger;
        private readonly IHubContext<MessengerHub> _hubContext;

        public MessageController(
            IMessageReadService messageReadService,
            IMessageWriteService messageWriteService,
            IChatReadService chatReadService,
            IUserReadService userReadService,
            ILogger<MessageController> logger,
            IHubContext<MessengerHub> hubContext)
        {
            _messageReadService = messageReadService;
            _messageWriteService = messageWriteService;
            _chatReadService = chatReadService;
            _userReadService = userReadService;
            _logger = logger;
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
        public async Task<IActionResult> GetAll()
        {
            var currentUserId = GetCurrentUserId();
            var messages = await _messageReadService.GetAllMessagesAsync(currentUserId);
            return Ok(messages);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var message = await _messageReadService.GetMessageByIdAsync(id, currentUserId);

            if (message == null)
                return NotFound(new { message = $"Сообщение с Id {id} не найдено" });

            return Ok(message);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MessageCreateDTO messageCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();

            if (currentUserId != messageCreateDto.UserId)
                return Forbid("Вы не можете создавать сообщения от имени другого пользователя");

            var userInChat = await _chatReadService.UserInChatAsync(messageCreateDto.ChatId, currentUserId);
            if (!userInChat)
                return Forbid("Вы не в этом чате");

            var message = await _messageWriteService.CreateMessageAsync(
                messageCreateDto.UserId,
                messageCreateDto.ChatId,
                messageCreateDto.MessageText);

            if (message == null)
                return BadRequest(new { message = "Не удалось отправить сообщение" });

            var currentUser = await _userReadService.GetProfileAsync(currentUserId);
            var currentUserName = currentUser?.Name ?? "User";

            await _hubContext.Clients.Group(messageCreateDto.ChatId.ToString()).SendAsync("ReceiveMessage",
                currentUserId.ToString(),
                currentUserName,
                messageCreateDto.MessageText,
                messageCreateDto.ChatId.ToString());

            return CreatedAtAction(nameof(GetById), new { id = message.MessageId }, message);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] MessageUpdateDTO messageUpdateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (id != messageUpdateDto.MessageId)
                return BadRequest("ID mismatch");

            var currentUserId = GetCurrentUserId();
            MessageResponseDTO? updatedMessage = null;
            
            // Если есть зашифрованные данные - обновляем как зашифрованное
            if (!string.IsNullOrEmpty(messageUpdateDto.EncryptedData) && !string.IsNullOrEmpty(messageUpdateDto.Iv))
            {
                updatedMessage = await _messageWriteService.UpdateEncryptedMessageAsync(
                    id, 
                    messageUpdateDto.EncryptedData, 
                    messageUpdateDto.Iv, 
                    currentUserId);
            }
            // Иначе обновляем как обычное сообщение
            else if (!string.IsNullOrEmpty(messageUpdateDto.MessageText))
            {
                updatedMessage = await _messageWriteService.UpdateMessageAsync(
                    id, 
                    messageUpdateDto.MessageText, 
                    currentUserId);
            }
            else
            {
                return BadRequest(new { message = "No content to update" });
            }

            if (updatedMessage == null)
                return NotFound(new { message = $"Message with Id {id} not found or no permission" });

            return Ok(updatedMessage);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _messageWriteService.DeleteMessageAsync(id, currentUserId);

            if (!result)
                return NotFound(new { message = $"Сообщение с Id {id} не найдено или нет прав" });

            return Ok(new { message = "Сообщение успешно удалено", id });
        }

        [HttpDelete("permanent/{id}")]
        public async Task<IActionResult> PermanentDelete(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _messageWriteService.PermanentDeleteMessageAsync(id, currentUserId);

            if (!result)
                return NotFound(new { message = $"Сообщение с Id {id} не найдено или нет прав" });

            return Ok(new { message = "Сообщение полностью удалено", id });
        }

        [HttpPatch("restore/{id}")]
        public async Task<IActionResult> Restore(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _messageWriteService.RestoreMessageAsync(id, currentUserId);

            if (!result)
                return NotFound(new { message = $"Сообщение с Id {id} не найдено или нет прав" });

            return Ok(new { message = "Сообщение успешно восстановлено", id });
        }

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadCounts()
        {
            var userId = GetCurrentUserId();
            var counts = await _messageReadService.GetUnreadCountsAsync(userId);
            return Ok(counts);
        }

        [HttpPost("{chatId}/mark-read")]
        public async Task<IActionResult> MarkAsRead(Guid chatId)
        {
            var userId = GetCurrentUserId();
            await _messageWriteService.MarkMessagesAsReadAsync(chatId, userId);
            return Ok();
        }

        [HttpPost("encrypted")]
        public async Task<IActionResult> CreateEncrypted([FromBody] EncryptedMessageCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();

            if (currentUserId != dto.UserId)
                return Forbid("Вы не можете создавать сообщения от имени другого пользователя");

            var userInChat = await _chatReadService.UserInChatAsync(dto.ChatId, currentUserId);
            if (!userInChat)
                return Forbid("Вы не в этом чате");

            var message = await _messageWriteService.CreateEncryptedMessageAsync(
                dto.UserId,
                dto.ChatId,
                dto.EncryptedData,
                dto.Iv);

            if (message == null)
                return BadRequest(new { message = "Не удалось отправить сообщение" });

            var currentUser = await _userReadService.GetProfileAsync(currentUserId);
            var currentUserName = currentUser?.Name ?? "User";

            await _hubContext.Clients.Group(dto.ChatId.ToString()).SendAsync("ReceiveEncryptedMessage",
                currentUserId.ToString(),
                currentUserName,
                dto.EncryptedData,
                dto.Iv,
                dto.ChatId.ToString());

            return CreatedAtAction(nameof(GetById), new { id = message.MessageId }, message);
        }

        [HttpGet("deleted/{chatId}")]
        public async Task<IActionResult> GetDeletedMessages(Guid chatId)
        {
            var userId = GetCurrentUserId();
            var messages = await _messageReadService.GetDeletedMessagesAsync(chatId, userId);
            return Ok(messages);
        }
    }
}
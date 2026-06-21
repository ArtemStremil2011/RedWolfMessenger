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
    public class FileController : ControllerBase
    {
        private readonly IFileReadService _fileReadService;
        private readonly IFileWriteService _fileWriteService;
        private readonly ILogger<FileController> _logger;
        private readonly IHubContext<MessengerHub> _hubContext;

        public FileController(
            IFileReadService fileReadService,
            IFileWriteService fileWriteService,
            ILogger<FileController> logger,
            IHubContext<MessengerHub> hubContext)
        {
            _fileReadService = fileReadService;
            _fileWriteService = fileWriteService;
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

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] FileMessageCreateDTO dto)
        {
            try
            {
                if (dto.File == null || dto.File.Length == 0)
                    return BadRequest(new { message = "Файл не выбран" });

                var currentUserId = GetCurrentUserId();

                // ===== ДОБАВЛЯЕМ ПОДДЕРЖКУ ГОЛОСОВЫХ =====
                var isVoice = dto.IsVoice ?? false;
                var duration = dto.Duration ?? 0;

                _logger.LogInformation($"📤 Upload request: File={dto.File.FileName}, IsVoice={isVoice}, Duration={duration}");

                var result = await _fileWriteService.UploadFileAsync(
                    dto.ChatId,
                    dto.File,
                    dto.Caption,
                    currentUserId,
                    isVoice,
                    duration);

                if (result == null)
                    return BadRequest(new { message = "Не удалось загрузить файл" });

                // ===== ОТПРАВЛЯЕМ УВЕДОМЛЕНИЕ ЧЕРЕЗ SIGNALR =====
                await _hubContext.Clients.Group(dto.ChatId.ToString()).SendAsync("NewFileUploaded", result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
            }
        }

        [HttpGet("download/{messageId}")]
        public async Task<IActionResult> DownloadFile(Guid messageId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var hasAccess = await _fileReadService.UserHasAccessToFileAsync(messageId, currentUserId);
                if (!hasAccess)
                    return Forbid();

                var fileMessage = await _fileReadService.GetFileMessageAsync(messageId, currentUserId);
                if (fileMessage == null)
                    return NotFound(new { message = "Файл не найден" });

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileMessage.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { message = "Файл не найден на сервере" });

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, fileMessage.ContentType, fileMessage.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {MessageId}", messageId);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
            }
        }

        [HttpGet("chat/{chatId}")]
        public async Task<IActionResult> GetChatFiles(Guid chatId)
        {
            var currentUserId = GetCurrentUserId();
            var files = await _fileReadService.GetChatFilesAsync(chatId, currentUserId);
            return Ok(files);
        }

        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteFile(Guid messageId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _fileWriteService.DeleteFileAsync(messageId, currentUserId);

            if (!result)
                return NotFound(new { message = "Файл не найден или нет прав" });

            return Ok(new { message = "Файл удалён" });
        }
    }
}
using Blackjack.Services;
using Microsoft.AspNetCore.Mvc;

namespace Blackjack.Controllers
{
    [Route("[controller]")]
    public class AIController : Controller
    {
        private const int MaxMessageLength = 2000;
        private const int MaxImagePayloadLength = 12_000_000;

        private readonly OpenRouterService _openRouterService;
        private readonly ILogger<AIController> _logger;

        public AIController(OpenRouterService openRouterService, ILogger<AIController> logger)
        {
            _openRouterService = openRouterService;
            _logger = logger;
        }

        [HttpPost("Chat")]
        public async Task<IActionResult> Chat([FromBody] AIChatRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            var message = request.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new { success = false, error = "Mesaj boş olamaz." });
            }

            if (message.Length > MaxMessageLength)
            {
                return BadRequest(new { success = false, error = $"Mesaj en fazla {MaxMessageLength} karakter olabilir." });
            }

            try
            {
                var response = await _openRouterService.GetChatResponseAsync(message, request.GameState);
                return Json(new { success = true, response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI chat request failed.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "İşlem sırasında beklenmeyen bir hata oluştu." });
            }
        }

        [HttpPost("DetectCards")]
        public async Task<IActionResult> DetectCards([FromBody] CardDetectionRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            var image = request.Image?.Trim();
            if (string.IsNullOrWhiteSpace(image))
            {
                return BadRequest(new { success = false, error = "Görüntü verisi boş." });
            }

            if (image.Length > MaxImagePayloadLength)
            {
                return BadRequest(new { success = false, error = "Görüntü verisi çok büyük." });
            }

            try
            {
                var result = await _openRouterService.DetectCardsFromImageAsync(image);
                return Json(new
                {
                    success = result.Success,
                    cards = result.Cards,
                    message = result.Message,
                    error = result.Error
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI card detection request failed.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "Kart algılama sırasında beklenmeyen bir hata oluştu." });
            }
        }

        [HttpPost("PokerChat")]
        public async Task<IActionResult> PokerChat([FromBody] AIChatRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            var message = request.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new { success = false, error = "Mesaj boş olamaz." });
            }

            if (message.Length > MaxMessageLength)
            {
                return BadRequest(new { success = false, error = $"Mesaj en fazla {MaxMessageLength} karakter olabilir." });
            }

            try
            {
                var response = await _openRouterService.GetPokerChatResponseAsync(message, request.GameState);
                return Json(new { success = true, response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI poker chat request failed.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "İşlem sırasında beklenmeyen bir hata oluştu." });
            }
        }
    }

    public class AIChatRequest
    {
        public string Message { get; set; } = "";
        public object? GameState { get; set; }
    }

    public class CardDetectionRequest
    {
        public string Image { get; set; } = "";
    }
}

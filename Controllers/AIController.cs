using Microsoft.AspNetCore.Mvc;
using Blackjack.Services;

namespace Blackjack.Controllers
{
    [Route("[controller]")]
    public class AIController : Controller
    {
        private readonly OpenRouterService _openRouterService;

        public AIController(OpenRouterService openRouterService)
        {
            _openRouterService = openRouterService;
        }

        [HttpPost("Chat")]
        public async Task<IActionResult> Chat([FromBody] AIChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, error = "Mesaj boş olamaz." });
            }

            try
            {
                var response = await _openRouterService.GetChatResponseAsync(
                    request.Message,
                    request.GameState
                );

                return Json(new { success = true, response });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Bir hata oluştu: " + ex.Message });
            }
        }

        [HttpPost("DetectCards")]
        public async Task<IActionResult> DetectCards([FromBody] CardDetectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Image))
            {
                return Json(new { success = false, error = "Görüntü verisi boş." });
            }

            try
            {
                var result = await _openRouterService.DetectCardsFromImageAsync(request.Image);
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
                return Json(new { success = false, error = "Kart algılama hatası: " + ex.Message });
            }
        }

        [HttpPost("PokerChat")]
        public async Task<IActionResult> PokerChat([FromBody] AIChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, error = "Mesaj boş olamaz." });
            }

            try
            {
                var response = await _openRouterService.GetPokerChatResponseAsync(
                    request.Message,
                    request.GameState
                );

                return Json(new { success = true, response });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Bir hata oluştu: " + ex.Message });
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

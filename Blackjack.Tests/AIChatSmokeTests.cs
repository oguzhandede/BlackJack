using Blackjack.Controllers;
using Blackjack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class AIChatSmokeTests
{
    [Fact]
    public async Task Chat_ReturnsSuccessfulResponse_WithFakeAiService()
    {
        var controller = new AIController(new FakeAIService(), NullLogger<AIController>.Instance);

        var result = await controller.Chat(new AIChatRequest
        {
            Message = "Kisa bir tavsiye ver."
        });

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonTestHelpers.ToJsonNode(json.Value);
        Assert.True(payload["success"]!.GetValue<bool>());
        Assert.Equal("Mocked AI reply", payload["response"]!.GetValue<string>());
    }

    private sealed class FakeAIService : IAIService
    {
        public Task<string> GetChatResponseAsync(string userMessage, object? gameState = null)
        {
            return Task.FromResult("Mocked AI reply");
        }

        public Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null)
        {
            return Task.FromResult("Mocked poker AI reply");
        }

        public Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image)
        {
            return Task.FromResult(new CardDetectionResult { Success = true });
        }
    }
}

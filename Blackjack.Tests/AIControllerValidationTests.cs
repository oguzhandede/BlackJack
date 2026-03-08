using Blackjack.Controllers;
using Blackjack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class AIControllerValidationTests
{
    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenRequestIsNull()
    {
        var controller = CreateController();

        var result = await controller.Chat(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonTestHelpers.ToJsonNode(badRequest.Value);
        Assert.False(payload["success"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenMessageIsTooLong()
    {
        var controller = CreateController();
        var request = new AIChatRequest
        {
            Message = new string('a', 2001)
        };

        var result = await controller.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DetectCards_ReturnsBadRequest_WhenImagePayloadIsTooLarge()
    {
        var controller = CreateController();
        var request = new CardDetectionRequest
        {
            Image = new string('x', 12_000_001)
        };

        var result = await controller.DetectCards(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AIController CreateController()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouter:ApiKey"] = "test-api-key",
                ["OpenRouter:BaseUrl"] = "https://openrouter.ai/api/v1"
            })
            .Build();

        var service = new OpenRouterService(new HttpClient(), configuration, NullLogger<OpenRouterService>.Instance);
        return new AIController(service, NullLogger<AIController>.Instance);
    }
}

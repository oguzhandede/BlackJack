using System.Net;
using System.Text;
using Blackjack.Controllers;
using Blackjack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class AIChatSmokeTests
{
    [Fact]
    public async Task Chat_ReturnsSuccessfulResponse_WithFakeOpenRouterService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouter:ApiKey"] = "fake-api-key",
                ["OpenRouter:BaseUrl"] = "https://example.test"
            })
            .Build();

        var fakeHandler = new FakeOpenRouterHandler();
        var httpClient = new HttpClient(fakeHandler);
        var service = new OpenRouterService(httpClient, configuration, NullLogger<OpenRouterService>.Instance);
        var controller = new AIController(service, NullLogger<AIController>.Instance);

        var result = await controller.Chat(new AIChatRequest
        {
            Message = "Kisa bir tavsiye ver."
        });

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonTestHelpers.ToJsonNode(json.Value);
        Assert.True(payload["success"]!.GetValue<bool>());
        Assert.Equal("Mocked AI reply", payload["response"]!.GetValue<string>());
    }

    private sealed class FakeOpenRouterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/chat/completions", request.RequestUri?.AbsolutePath);

            const string content = "{\"choices\":[{\"message\":{\"content\":\"Mocked AI reply\"}}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}

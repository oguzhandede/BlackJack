using System.Net;
using System.Text;
using Blackjack.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class ConfiguredAIServiceTests
{
    [Fact]
    public async Task Chat_UsesAnythingLlm_WhenProviderIsConfigured()
    {
        var configuration = BuildConfiguration("AnythingLLM");
        var service = CreateService(configuration);

        var response = await service.GetChatResponseAsync("Merhaba");

        Assert.Equal("anythingllm", response);
    }

    [Fact]
    public async Task Chat_FallsBackToOpenRouter_WhenProviderIsUnknown()
    {
        var configuration = BuildConfiguration("UnknownProvider");
        var service = CreateService(configuration);

        var response = await service.GetChatResponseAsync("Merhaba");

        Assert.Equal("openrouter", response);
    }

    private static ConfiguredAIService CreateService(IConfiguration configuration)
    {
        var openRouter = new OpenRouterService(
            new HttpClient(new FakeOpenRouterHandler()),
            configuration,
            NullLogger<OpenRouterService>.Instance);

        var anythingLlm = new AnythingLlmService(
            new HttpClient(new FakeAnythingLlmHandler()),
            configuration,
            NullLogger<AnythingLlmService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        return new ConfiguredAIService(
            openRouter,
            anythingLlm,
            configuration,
            NullLogger<ConfiguredAIService>.Instance);
    }

    private static IConfiguration BuildConfiguration(string provider)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = provider,
                ["OpenRouter:ApiKey"] = "openrouter-key",
                ["OpenRouter:BaseUrl"] = "https://openrouter.test",
                ["AnythingLLM:BaseUrl"] = "http://anythingllm.test",
                ["AnythingLLM:ApiKey"] = "anythingllm-key",
                ["AnythingLLM:WorkspaceSlug"] = "poyo",
                ["AnythingLLM:Mode"] = "query",
                ["AnythingLLM:HistoryLimit"] = "0"
            })
            .Build();
    }

    private sealed class FakeOpenRouterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"openrouter\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class FakeAnythingLlmHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"textResponse\":\"anythingllm\",\"error\":null}",
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }
}

using System.Net;
using System.Text;
using Blackjack.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class AnythingLlmServiceTests
{
    [Fact]
    public async Task Chat_ReturnsTextResponse_FromAnythingLlm()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnythingLLM:BaseUrl"] = "http://anythingllm.test",
                ["AnythingLLM:ApiKey"] = "test-api-key",
                ["AnythingLLM:WorkspaceSlug"] = "poyo",
                ["AnythingLLM:Mode"] = "query",
                ["AnythingLLM:HistoryLimit"] = "0",
                ["AnythingLLM:HistoryOrderBy"] = "asc"
            })
            .Build();

        var httpClient = new HttpClient(new FakeAnythingLlmHandler())
        {
            BaseAddress = new Uri("http://anythingllm.test")
        };

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var service = new AnythingLlmService(
            httpClient,
            configuration,
            NullLogger<AnythingLlmService>.Instance,
            httpContextAccessor);

        var response = await service.GetChatResponseAsync("Kisa bir tavsiye ver.");

        Assert.Equal("AnythingLLM reply", response);
    }

    private sealed class FakeAnythingLlmHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/workspace/poyo/chat", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-api-key", request.Headers.Authorization?.Parameter);

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("\"mode\":\"query\"", body);
            Assert.Contains("\"message\":", body);
            Assert.Contains("KULLANICI MESAJI", body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"textResponse\":\"AnythingLLM reply\",\"error\":null}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}

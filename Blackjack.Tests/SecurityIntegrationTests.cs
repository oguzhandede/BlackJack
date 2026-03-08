using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Blackjack.Tests;

public class SecurityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex TokenRegex = new(
        "<meta\\s+name=\"request-verification-token\"\\s+content=\"([^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly WebApplicationFactory<Program> _factory;

    public SecurityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDeck_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.PostAsync(
            "/Blackjack/CreateDeck",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["numberOfDecks"] = "6"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeck_WithAntiforgeryToken_RedirectsToManageDeck()
    {
        var tokenClient = await CreateClientWithAntiforgeryTokenAsync();
        using var client = tokenClient.Client;
        var token = tokenClient.Token;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Blackjack/CreateDeck")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["numberOfDecks"] = "6"
            })
        };
        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/blackjack/ManageDeck", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Chat_WithMalformedJson_ReturnsBadRequest()
    {
        var tokenClient = await CreateClientWithAntiforgeryTokenAsync();
        using var client = tokenClient.Client;
        var token = tokenClient.Token;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat")
        {
            Content = new StringContent("{\"message\":", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Client, string Token)> CreateClientWithAntiforgeryTokenAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/Blackjack");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, "Antiforgery token meta tag was not found.");

        var token = WebUtility.HtmlDecode(match.Groups[1].Value);
        return (client, token);
    }
}

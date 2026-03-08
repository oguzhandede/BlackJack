using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Blackjack.Tests;

public class RegressionSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex TokenRegex = new(
        "<meta\\s+name=\"request-verification-token\"\\s+content=\"([^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly WebApplicationFactory<Program> _factory;

    public RegressionSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeckCreateAndRemoveCards_Smoke()
    {
        var tokenClient = await CreateClientWithTokenAsync("/Blackjack");
        using var client = tokenClient.Client;

        using var createDeckRequest = new HttpRequestMessage(HttpMethod.Post, "/Blackjack/CreateDeck")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["numberOfDecks"] = "2"
            })
        };
        createDeckRequest.Headers.Add("RequestVerificationToken", tokenClient.Token);

        var createDeckResponse = await client.SendAsync(createDeckRequest);
        Assert.Equal(HttpStatusCode.Redirect, createDeckResponse.StatusCode);
        Assert.Equal("/blackjack/ManageDeck", createDeckResponse.Headers.Location?.ToString());

        var manageDeckToken = await ReadAntiforgeryTokenFromPageAsync(client, "/Blackjack/ManageDeck");
        using var removeCardsRequest = new HttpRequestMessage(HttpMethod.Post, "/Blackjack/RemoveCards")
        {
            Content = new StringContent(
                "{\"cards\":[{\"suit\":\"Kupa\",\"rank\":\"As\"}]}",
                Encoding.UTF8,
                "application/json")
        };
        removeCardsRequest.Headers.Add("RequestVerificationToken", manageDeckToken);

        var removeCardsResponse = await client.SendAsync(removeCardsRequest);
        Assert.Equal(HttpStatusCode.OK, removeCardsResponse.StatusCode);

        var removeCardsJson = JsonNode.Parse(await removeCardsResponse.Content.ReadAsStringAsync());
        Assert.NotNull(removeCardsJson);
        Assert.True(removeCardsJson!["success"]!.GetValue<bool>());
        Assert.True(removeCardsJson["removed"]!.GetValue<int>() >= 0);
    }

    [Fact]
    public async Task PokerActionFlow_Smoke()
    {
        var tokenClient = await CreateClientWithTokenAsync("/Poker");
        using var client = tokenClient.Client;

        using var newGameRequest = new HttpRequestMessage(HttpMethod.Post, "/Poker/NewGame")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["botCount"] = "1",
                ["startingChips"] = "1000",
                ["blindLevel"] = "1"
            })
        };
        newGameRequest.Headers.Add("RequestVerificationToken", tokenClient.Token);

        var newGameResponse = await client.SendAsync(newGameRequest);
        Assert.Equal(HttpStatusCode.Redirect, newGameResponse.StatusCode);
        Assert.Equal("/Poker/Table", newGameResponse.Headers.Location?.ToString());

        var tableToken = await ReadAntiforgeryTokenFromPageAsync(client, "/Poker/Table");
        using var actionRequest = new HttpRequestMessage(HttpMethod.Post, "/Poker/Action")
        {
            Content = new StringContent(
                "{\"action\":\"Fold\",\"amount\":0}",
                Encoding.UTF8,
                "application/json")
        };
        actionRequest.Headers.Add("RequestVerificationToken", tableToken);

        var actionResponse = await client.SendAsync(actionRequest);
        Assert.Equal(HttpStatusCode.OK, actionResponse.StatusCode);

        var actionJson = JsonNode.Parse(await actionResponse.Content.ReadAsStringAsync());
        Assert.NotNull(actionJson);
        Assert.True(actionJson!["success"]!.GetValue<bool>());
    }

    private async Task<(HttpClient Client, string Token)> CreateClientWithTokenAsync(string tokenPagePath)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await ReadAntiforgeryTokenFromPageAsync(client, tokenPagePath);
        return (client, token);
    }

    private static async Task<string> ReadAntiforgeryTokenFromPageAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, $"Antiforgery token meta tag was not found on {path}.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}

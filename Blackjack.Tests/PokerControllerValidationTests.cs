using Blackjack.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class PokerControllerValidationTests
{
    [Fact]
    public void Action_ReturnsBadRequest_WhenActionNameIsInvalid()
    {
        var controller = CreateController();
        var request = new PokerActionRequest
        {
            Action = "Hack",
            Amount = 10
        };

        var result = controller.Action(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonTestHelpers.ToJsonNode(badRequest.Value);
        Assert.False(payload["success"]!.GetValue<bool>());
    }

    [Fact]
    public void AnalyzeCards_ReturnsBadRequest_WhenRequestIsNull()
    {
        var controller = CreateController();

        var result = controller.AnalyzeCards(null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void AnalyzeCards_ReturnsBadRequest_WhenOpponentCountOutOfRange()
    {
        var controller = CreateController();
        var request = new LiveAnalyzeRequest
        {
            HoleCards = new List<CardInput>
            {
                new() { Suit = "Kupa", Rank = "As" },
                new() { Suit = "Sinek", Rank = "Papaz" }
            },
            OpponentCount = 0
        };

        var result = controller.AnalyzeCards(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void AnalyzeCards_ReturnsBadRequest_WhenCardValuesInvalid()
    {
        var controller = CreateController();
        var request = new LiveAnalyzeRequest
        {
            HoleCards = new List<CardInput>
            {
                new() { Suit = "InvalidSuit", Rank = "As" },
                new() { Suit = "Sinek", Rank = "Papaz" }
            }
        };

        var result = controller.AnalyzeCards(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static PokerController CreateController()
    {
        return new PokerController(NullLogger<PokerController>.Instance);
    }
}

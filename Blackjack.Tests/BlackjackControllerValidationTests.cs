using Blackjack.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blackjack.Tests;

public class BlackjackControllerValidationTests
{
    [Fact]
    public void RemoveCards_ReturnsBadRequest_WhenRequestIsNull()
    {
        var controller = new BlackjackController(NullLogger<BlackjackController>.Instance);

        var result = controller.RemoveCards(null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void RemoveCards_ReturnsBadRequest_WhenMoreThan52CardsProvided()
    {
        var controller = new BlackjackController(NullLogger<BlackjackController>.Instance);
        var request = new RemoveCardsRequest
        {
            Cards = Enumerable.Range(0, 53)
                .Select(_ => new CardItem { Suit = "Kupa", Rank = "As" })
                .ToList()
        };

        var result = controller.RemoveCards(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}

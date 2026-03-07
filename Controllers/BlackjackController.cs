using Microsoft.AspNetCore.Mvc;
using Blackjack.Models;
using System.Text.Json;

namespace Blackjack.Controllers
{
    public class BlackjackController : Controller
    {
        private const string GameSessionKey = "BlackjackGame";

        private BlackjackGame? GetGame()
        {
            var json = HttpContext.Session.GetString(GameSessionKey);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<BlackjackGame>(json);
        }

        private void SaveGame(BlackjackGame game)
        {
            var json = JsonSerializer.Serialize(game);
            HttpContext.Session.SetString(GameSessionKey, json);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateDeck(int numberOfDecks)
        {
            if (numberOfDecks < 1) numberOfDecks = 1;
            if (numberOfDecks > 8) numberOfDecks = 8;

            var game = new BlackjackGame(numberOfDecks);
            SaveGame(game);
            return RedirectToAction("ManageDeck");
        }

        public IActionResult ManageDeck()
        {
            var game = GetGame();
            if (game == null) return RedirectToAction("Index");

            var probabilities = game.CalculateProbabilities();
            ViewData["Probabilities"] = probabilities;
            return View(game);
        }

        [HttpPost]
        public IActionResult RemoveCard(string suit, string rank)
        {
            var game = GetGame();
            if (game == null) return RedirectToAction("Index");

            if (game.Deck.CardExists(suit, rank))
            {
                game.RemoveCard(suit, rank);
                SaveGame(game);
            }
            return RedirectToAction("ManageDeck");
        }

        [HttpPost]
        public IActionResult AddCardBack(string suit, string rank)
        {
            var game = GetGame();
            if (game == null) return RedirectToAction("Index");

            game.AddCardBack(suit, rank);
            SaveGame(game);
            return RedirectToAction("ManageDeck");
        }

        [HttpPost]
        public IActionResult ResetDeck()
        {
            HttpContext.Session.Remove(GameSessionKey);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveCards([FromBody] RemoveCardsRequest request)
        {
            var game = GetGame();
            if (game == null)
                return Json(new { success = false, error = "Oyun bulunamadı." });

            int removed = 0;
            var removedList = new List<object>();

            foreach (var card in request.Cards)
            {
                if (game.Deck.CardExists(card.Suit, card.Rank))
                {
                    game.RemoveCard(card.Suit, card.Rank);
                    removed++;
                    removedList.Add(new { card.Suit, card.Rank });
                }
            }

            SaveGame(game);

            return Json(new
            {
                success = true,
                removed,
                removedCards = removedList,
                remainingCount = game.Deck.Cards.Count,
                totalRemoved = game.RemovedCards.Count
            });
        }
    }

    public class RemoveCardsRequest
    {
        public List<CardItem> Cards { get; set; } = new();
    }

    public class CardItem
    {
        public string Suit { get; set; } = "";
        public string Rank { get; set; } = "";
    }
}

using Microsoft.AspNetCore.Mvc;
using Blackjack.Models;
using System.Text.Json;

namespace Blackjack.Controllers
{
    public class BlackjackController : Controller
    {
        private const string GameSessionKey = "BlackjackGame";
        private static readonly HashSet<string> ValidSuits = new() { "Kupa", "Karo", "Sinek", "Maça" };
        private static readonly HashSet<string> ValidRanks = new() { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };
        private readonly ILogger<BlackjackController> _logger;

        public BlackjackController(ILogger<BlackjackController> logger)
        {
            _logger = logger;
        }

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
        public IActionResult RemoveCards([FromBody] RemoveCardsRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            if (request.Cards == null || request.Cards.Count == 0)
            {
                return BadRequest(new { success = false, error = "En az bir kart göndermelisiniz." });
            }

            if (request.Cards.Count > 52)
            {
                return BadRequest(new { success = false, error = "Tek istekte en fazla 52 kart gönderilebilir." });
            }

            if (request.Cards.Any(c =>
                    c == null ||
                    string.IsNullOrWhiteSpace(c.Suit) ||
                    string.IsNullOrWhiteSpace(c.Rank) ||
                    !ValidSuits.Contains(c.Suit.Trim()) ||
                    !ValidRanks.Contains(c.Rank.Trim())))
            {
                return BadRequest(new { success = false, error = "Kart bilgileri geçersiz." });
            }

            var game = GetGame();
            if (game == null)
            {
                return NotFound(new { success = false, error = "Oyun bulunamadı." });
            }

            try
            {
                int removed = 0;
                var removedList = new List<object>();

                foreach (var card in request.Cards)
                {
                    var suit = card.Suit.Trim();
                    var rank = card.Rank.Trim();
                    if (game.Deck.CardExists(suit, rank))
                    {
                        game.RemoveCard(suit, rank);
                        removed++;
                        removedList.Add(new { Suit = suit, Rank = rank });
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove cards from blackjack game.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "Kart çıkarma sırasında beklenmeyen bir hata oluştu." });
            }
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

using Blackjack.Models.Poker;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Blackjack.Controllers
{
    public class PokerController : Controller
    {
        private const string PokerSessionKey = "PokerGame";
        private const int MaxNumericInput = 10_000_000;
        private static readonly HashSet<PlayerAction> AllowedActions = new()
        {
            PlayerAction.Fold,
            PlayerAction.Check,
            PlayerAction.Call,
            PlayerAction.Raise,
            PlayerAction.AllIn
        };
        private static readonly HashSet<string> ValidSuits = new() { "Kupa", "Karo", "Sinek", "Maça" };
        private static readonly HashSet<string> ValidRanks = new() { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };

        private readonly ILogger<PokerController> _logger;

        public PokerController(ILogger<PokerController> logger)
        {
            _logger = logger;
        }

        private PokerGame? GetGame()
        {
            var json = HttpContext.Session.GetString(PokerSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PokerGame>(json);
        }

        private void SaveGame(PokerGame game)
        {
            var json = JsonSerializer.Serialize(game);
            HttpContext.Session.SetString(PokerSessionKey, json);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult NewGame(int botCount = 3, int startingChips = 1000, int blindLevel = 1)
        {
            if (botCount < 1) botCount = 1;
            if (botCount > 5) botCount = 5;
            if (startingChips < 100) startingChips = 100;

            var game = new PokerGame
            {
                SmallBlindAmount = blindLevel switch
                {
                    1 => 5,
                    2 => 10,
                    3 => 25,
                    4 => 50,
                    _ => 10
                }
            };

            game.BigBlindAmount = game.SmallBlindAmount * 2;
            game.SetupGame(botCount, startingChips);
            game.StartNewHand();
            SaveGame(game);

            return RedirectToAction("Table");
        }

        public IActionResult Table()
        {
            var game = GetGame();
            if (game == null)
            {
                return RedirectToAction("Index");
            }

            var human = game.HumanPlayer;
            if (human.HoleCards.Count >= 2)
            {
                var strategy = PokerStrategy.Analyze(
                    human.HoleCards,
                    game.CommunityCards,
                    game.Phase,
                    game.Pot,
                    game.CurrentBet,
                    human.CurrentBet,
                    human.Chips,
                    game.ActivePlayers.Count
                );
                ViewData["Strategy"] = strategy;
            }

            return View(game);
        }

        [HttpPost]
        public IActionResult Action([FromBody] PokerActionRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            if (string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest(new { success = false, error = "Aksiyon boş olamaz." });
            }

            if (request.Action.Length > 20)
            {
                return BadRequest(new { success = false, error = "Aksiyon geçersiz." });
            }

            if (request.Amount < 0 || request.Amount > MaxNumericInput)
            {
                return BadRequest(new { success = false, error = "Bahis miktarı geçersiz." });
            }

            if (!Enum.TryParse<PlayerAction>(request.Action.Trim(), true, out var action) || !AllowedActions.Contains(action))
            {
                return BadRequest(new { success = false, error = "Geçersiz aksiyon." });
            }

            var game = GetGame();
            if (game == null)
            {
                return NotFound(new { success = false, error = "Oyun bulunamadı." });
            }

            if (!game.IsHumanTurn)
            {
                return BadRequest(new { success = false, error = "Sıra sizde değil." });
            }

            try
            {
                var success = game.PerformAction(action, request.Amount);
                if (!success)
                {
                    return BadRequest(new { success = false, error = "Geçersiz aksiyon." });
                }

                game.RunPendingBotActions();
                SaveGame(game);
                return Json(new { success = true, redirect = Url.Action("Table") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Poker action request failed.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "Aksiyon işlenirken beklenmeyen bir hata oluştu." });
            }
        }

        [HttpPost]
        public IActionResult NextHand()
        {
            var game = GetGame();
            if (game == null) return RedirectToAction("Index");

            if (game.IsGameOver)
            {
                return RedirectToAction("Index");
            }

            game.StartNewHand();
            SaveGame(game);

            return RedirectToAction("Table");
        }

        [HttpGet]
        public IActionResult GetState()
        {
            var game = GetGame();
            if (game == null)
            {
                return Json(new { success = false });
            }

            var human = game.HumanPlayer;
            StrategyResult? strategy = null;

            if (human.HoleCards.Count >= 2)
            {
                strategy = PokerStrategy.Analyze(
                    human.HoleCards,
                    game.CommunityCards,
                    game.Phase,
                    game.Pot,
                    game.CurrentBet,
                    human.CurrentBet,
                    human.Chips,
                    game.ActivePlayers.Count
                );
            }

            return Json(new
            {
                success = true,
                phase = game.Phase.ToString(),
                pot = game.Pot,
                currentBet = game.CurrentBet,
                isHumanTurn = game.IsHumanTurn,
                canCheck = game.CanCheck,
                callAmount = game.CallAmount,
                minRaise = game.MinRaise,
                communityCards = game.CommunityCards.Select(c => new { c.Suit, c.Rank, c.SuitSymbol, c.DisplayName }),
                players = game.Players.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Chips,
                    p.IsBot,
                    p.HasFolded,
                    p.IsAllIn,
                    p.IsDealer,
                    p.CurrentBet,
                    p.PositionDisplay,
                    lastAction = p.LastAction.ToString(),
                    holeCards = p.IsBot && game.Phase != GamePhase.Showdown && game.Phase != GamePhase.Finished
                        ? null
                        : p.HoleCards.Select(c => new { c.Suit, c.Rank, c.SuitSymbol, c.DisplayName }),
                    handResult = p.HandResult != null ? new { p.HandResult.RankDisplayName, p.HandResult.Description } : null
                }),
                strategy = strategy != null ? new
                {
                    strategy.ChenScore,
                    strategy.HandStrengthPercent,
                    strategy.PotOdds,
                    strategy.Outs,
                    strategy.DrawProbability,
                    strategy.RecommendedAction,
                    strategy.Reasoning,
                    strategy.AggressionLevel,
                    strategy.Tips
                } : null,
                actionLog = game.ActionLog,
                winnerMessage = game.WinnerMessage,
                isGameOver = game.IsGameOver
            });
        }

        [HttpPost]
        public IActionResult Reset()
        {
            HttpContext.Session.Remove(PokerSessionKey);
            return RedirectToAction("Index");
        }

        public IActionResult Live()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AnalyzeCards([FromBody] LiveAnalyzeRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Geçersiz istek gövdesi." });
            }

            if (request.PotSize < 0 || request.PotSize > MaxNumericInput ||
                request.CurrentBet < 0 || request.CurrentBet > MaxNumericInput ||
                request.StackSize < 0 || request.StackSize > MaxNumericInput)
            {
                return BadRequest(new { success = false, error = "Sayısal değerler geçersiz." });
            }

            if (request.OpponentCount is < 1 or > 9)
            {
                return BadRequest(new { success = false, error = "Rakip sayısı 1 ile 9 arasında olmalıdır." });
            }

            if ((request.HoleCards?.Count ?? 0) > 2)
            {
                return BadRequest(new { success = false, error = "Hole card sayısı en fazla 2 olabilir." });
            }

            if ((request.CommunityCards?.Count ?? 0) > 5)
            {
                return BadRequest(new { success = false, error = "Community card sayısı en fazla 5 olabilir." });
            }

            if (!AreCardsValid(request.HoleCards) || !AreCardsValid(request.CommunityCards))
            {
                return BadRequest(new { success = false, error = "Kart bilgileri geçersiz." });
            }

            try
            {
                var holeCards = request.HoleCards?.Select(c => new PokerCard { Suit = c.Suit.Trim(), Rank = c.Rank.Trim() }).ToList()
                    ?? new List<PokerCard>();
                var communityCards = request.CommunityCards?.Select(c => new PokerCard { Suit = c.Suit.Trim(), Rank = c.Rank.Trim() }).ToList()
                    ?? new List<PokerCard>();

                var phase = communityCards.Count switch
                {
                    0 => GamePhase.PreFlop,
                    3 => GamePhase.Flop,
                    4 => GamePhase.Turn,
                    5 => GamePhase.River,
                    _ => GamePhase.PreFlop
                };

                if (holeCards.Count < 2)
                {
                    return Json(new
                    {
                        success = true,
                        needMoreCards = true,
                        message = "En az 2 hole card gerekli."
                    });
                }

                var strategy = PokerStrategy.Analyze(
                    holeCards,
                    communityCards,
                    phase,
                    request.PotSize,
                    request.CurrentBet,
                    0,
                    request.StackSize,
                    request.OpponentCount
                );

                HandResult? handResult = null;
                if (communityCards.Count >= 3)
                {
                    handResult = HandEvaluator.Evaluate(holeCards, communityCards);
                }

                return Json(new
                {
                    success = true,
                    needMoreCards = false,
                    phase = phase.ToString(),
                    strategy = new
                    {
                        strategy.ChenScore,
                        strategy.HandStrengthPercent,
                        strategy.PotOdds,
                        strategy.Outs,
                        strategy.DrawProbability,
                        strategy.RecommendedAction,
                        strategy.Reasoning,
                        strategy.AggressionLevel,
                        strategy.Tips
                    },
                    handResult = handResult != null ? new
                    {
                        handResult.RankDisplayName,
                        handResult.Description
                    } : null,
                    aiContext = PokerStrategy.FormatForAI(strategy, holeCards, communityCards, phase, request.PotSize, request.StackSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Poker live analysis request failed.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, error = "Analiz sırasında beklenmeyen bir hata oluştu." });
            }
        }

        private static bool AreCardsValid(IEnumerable<CardInput>? cards)
        {
            if (cards == null)
            {
                return true;
            }

            foreach (var card in cards)
            {
                if (card == null ||
                    string.IsNullOrWhiteSpace(card.Suit) ||
                    string.IsNullOrWhiteSpace(card.Rank) ||
                    !ValidSuits.Contains(card.Suit.Trim()) ||
                    !ValidRanks.Contains(card.Rank.Trim()))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class PokerActionRequest
    {
        public string Action { get; set; } = "";
        public int Amount { get; set; }
    }

    public class LiveAnalyzeRequest
    {
        public List<CardInput>? HoleCards { get; set; }
        public List<CardInput>? CommunityCards { get; set; }
        public int PotSize { get; set; }
        public int CurrentBet { get; set; }
        public int StackSize { get; set; } = 1000;
        public int OpponentCount { get; set; } = 3;
    }

    public class CardInput
    {
        public string Suit { get; set; } = "";
        public string Rank { get; set; } = "";
    }
}

using Microsoft.AspNetCore.Mvc;
using Blackjack.Models.Poker;
using System.Text.Json;

namespace Blackjack.Controllers
{
    public class PokerController : Controller
    {
        private const string PokerSessionKey = "PokerGame";

        private PokerGame? GetGame()
        {
            var json = HttpContext.Session.GetString(PokerSessionKey);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<PokerGame>(json);
        }

        private void SaveGame(PokerGame game)
        {
            var json = JsonSerializer.Serialize(game);
            HttpContext.Session.SetString(PokerSessionKey, json);
        }

        // ====================================
        // Lobby
        // ====================================
        public IActionResult Index()
        {
            return View();
        }

        // ====================================
        // New Game
        // ====================================
        [HttpPost]
        public IActionResult NewGame(int botCount = 3, int startingChips = 1000, int blindLevel = 1)
        {
            if (botCount < 1) botCount = 1;
            if (botCount > 5) botCount = 5;
            if (startingChips < 100) startingChips = 100;

            var game = new PokerGame();

            game.SmallBlindAmount = blindLevel switch
            {
                1 => 5,
                2 => 10,
                3 => 25,
                4 => 50,
                _ => 10
            };
            game.BigBlindAmount = game.SmallBlindAmount * 2;

            game.SetupGame(botCount, startingChips);
            game.StartNewHand();
            SaveGame(game);

            return RedirectToAction("Table");
        }

        // ====================================
        // Table (Main View)
        // ====================================
        public IActionResult Table()
        {
            var game = GetGame();
            if (game == null) return RedirectToAction("Index");

            // Calculate strategy for human player
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

        // ====================================
        // Player Action (AJAX)
        // ====================================
        [HttpPost]
        public IActionResult Action([FromBody] PokerActionRequest request)
        {
            var game = GetGame();
            if (game == null)
                return Json(new { success = false, error = "Oyun bulunamadı." });

            if (!game.IsHumanTurn)
                return Json(new { success = false, error = "Sıra sizde değil." });

            var action = Enum.Parse<PlayerAction>(request.Action, true);
            var success = game.PerformAction(action, request.Amount);

            if (success)
            {
                // Run any pending bot actions (e.g., after phase transitions)
                game.RunPendingBotActions();
                SaveGame(game);
                return Json(new { success = true, redirect = Url.Action("Table") });
            }

            return Json(new { success = false, error = "Geçersiz aksiyon." });
        }

        // ====================================
        // Next Hand
        // ====================================
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

        // ====================================
        // Get State (AJAX)
        // ====================================
        [HttpGet]
        public IActionResult GetState()
        {
            var game = GetGame();
            if (game == null)
                return Json(new { success = false });

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
                    p.Id, p.Name, p.Chips, p.IsBot,
                    p.HasFolded, p.IsAllIn, p.IsDealer,
                    p.CurrentBet, p.PositionDisplay,
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

        // ====================================
        // Reset Game
        // ====================================
        [HttpPost]
        public IActionResult Reset()
        {
            HttpContext.Session.Remove(PokerSessionKey);
            return RedirectToAction("Index");
        }

        // ====================================
        // Live Mode (Card Detection + Strategy)
        // ====================================
        public IActionResult Live()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AnalyzeCards([FromBody] LiveAnalyzeRequest request)
        {
            try
            {
                var holeCards = request.HoleCards?.Select(c => new PokerCard { Suit = c.Suit, Rank = c.Rank }).ToList()
                    ?? new List<PokerCard>();
                var communityCards = request.CommunityCards?.Select(c => new PokerCard { Suit = c.Suit, Rank = c.Rank }).ToList()
                    ?? new List<PokerCard>();

                // Determine phase from community cards count
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
                    0, // player's current bet (live mode, user hasn't bet yet)
                    request.StackSize,
                    request.OpponentCount
                );

                // Also evaluate the hand if community cards exist
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
                return Json(new { success = false, error = ex.Message });
            }
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

using System.Text.Json.Serialization;

namespace Blackjack.Models.Poker
{
    public class PokerGame
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public List<PokerPlayer> Players { get; set; } = new();
        public List<PokerCard> CommunityCards { get; set; } = new();
        public GamePhase Phase { get; set; } = GamePhase.Lobby;
        public int Pot { get; set; }
        public int CurrentBet { get; set; }
        public int SmallBlindAmount { get; set; } = 10;
        public int BigBlindAmount { get; set; } = 20;
        public int DealerIndex { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public int RoundNumber { get; set; }
        public string? WinnerMessage { get; set; }
        public List<string> ActionLog { get; set; } = new();

        // Deck state persisted for session serialization
        public List<PokerCard> DeckCards { get; set; } = new();
        public int DeckIndex { get; set; }

        [JsonIgnore]
        private PokerDeck? _deck;

        [JsonIgnore]
        public PokerDeck Deck
        {
            get
            {
                if (_deck == null)
                {
                    _deck = new PokerDeck();
                    // Restore deck state from serialized data
                    if (DeckCards.Count > 0)
                    {
                        _deck.LoadState(DeckCards, DeckIndex);
                    }
                }
                return _deck;
            }
            set
            {
                _deck = value;
                SaveDeckState();
            }
        }

        private void SaveDeckState()
        {
            if (_deck != null)
            {
                DeckCards = _deck.GetCards();
                DeckIndex = _deck.GetDealIndex();
            }
        }

        public PokerPlayer? CurrentPlayer =>
            CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count
                ? Players[CurrentPlayerIndex]
                : null;

        public PokerPlayer HumanPlayer => Players.First(p => !p.IsBot);

        public List<PokerPlayer> ActivePlayers => Players.Where(p => p.IsActive).ToList();

        // ====================================
        // Game Setup
        // ====================================
        public void SetupGame(int botCount, int startingChips)
        {
            Players.Clear();

            // Human player
            Players.Add(new PokerPlayer
            {
                Id = "human",
                Name = "Sen",
                IsBot = false,
                Chips = startingChips
            });

            // Bot players
            string[] botNames = { "Bot Ahmet", "Bot Mehmet", "Bot Ayşe", "Bot Fatma", "Bot Ali", "Bot Zeynep" };
            for (int i = 0; i < botCount && i < botNames.Length; i++)
            {
                Players.Add(new PokerPlayer
                {
                    Id = $"bot_{i}",
                    Name = botNames[i],
                    IsBot = true,
                    Chips = startingChips
                });
            }

            RoundNumber = 0;
            DealerIndex = 0;
        }

        // ====================================
        // New Hand
        // ====================================
        public void StartNewHand()
        {
            Deck = new PokerDeck();
            CommunityCards.Clear();
            Pot = 0;
            CurrentBet = 0;
            WinnerMessage = null;
            ActionLog.Clear();
            RoundNumber++;

            // Remove busted players
            Players.RemoveAll(p => p.IsBot && p.Chips <= 0);

            // Reset all players
            foreach (var player in Players)
                player.Reset();

            // Assign positions
            AssignPositions();

            // Post blinds
            PostBlinds();

            // Deal hole cards
            foreach (var player in Players)
            {
                player.HoleCards = Deck.Deal(2);
            }
            SaveDeckState();

            Phase = GamePhase.PreFlop;

            // Set first player (after big blind for preflop)
            SetFirstPlayerForPhase();

            AddLog($"🎴 El #{RoundNumber} başladı! Blind: {SmallBlindAmount}/{BigBlindAmount}");

            // Auto-run bot actions if first player is a bot
            RunPendingBotActions();
        }

        /// <summary>
        /// Runs bot actions in a loop until it's the human's turn or the hand is over.
        /// </summary>
        public void RunPendingBotActions()
        {
            int safety = 0;
            while (safety < 50 &&
                   CurrentPlayer != null &&
                   CurrentPlayer.IsBot &&
                   CurrentPlayer.IsActive &&
                   Phase != GamePhase.Finished &&
                   Phase != GamePhase.Showdown)
            {
                PerformBotAction(CurrentPlayer);
                safety++;
            }
        }

        private void AssignPositions()
        {
            var positions = Players.Count switch
            {
                2 => new[] { PlayerPosition.Button, PlayerPosition.BigBlind },
                3 => new[] { PlayerPosition.Button, PlayerPosition.SmallBlind, PlayerPosition.BigBlind },
                4 => new[] { PlayerPosition.Button, PlayerPosition.SmallBlind, PlayerPosition.BigBlind, PlayerPosition.UTG },
                5 => new[] { PlayerPosition.Button, PlayerPosition.SmallBlind, PlayerPosition.BigBlind, PlayerPosition.UTG, PlayerPosition.CutOff },
                _ => new[] { PlayerPosition.Button, PlayerPosition.SmallBlind, PlayerPosition.BigBlind, PlayerPosition.UTG, PlayerPosition.Middle, PlayerPosition.CutOff }
            };

            for (int i = 0; i < Players.Count; i++)
            {
                int idx = (DealerIndex + i) % Players.Count;
                Players[idx].Position = i < positions.Length ? positions[i] : PlayerPosition.Middle;
                Players[idx].IsDealer = idx == DealerIndex;
            }
        }

        private void PostBlinds()
        {
            int sbIndex, bbIndex;

            if (Players.Count == 2)
            {
                // Heads-up: dealer is SB
                sbIndex = DealerIndex;
                bbIndex = (DealerIndex + 1) % Players.Count;
            }
            else
            {
                sbIndex = (DealerIndex + 1) % Players.Count;
                bbIndex = (DealerIndex + 2) % Players.Count;
            }

            Players[sbIndex].PlaceBet(SmallBlindAmount);
            Pot += Players[sbIndex].CurrentBet;
            AddLog($"💰 {Players[sbIndex].Name} Small Blind: {SmallBlindAmount}");

            Players[bbIndex].PlaceBet(BigBlindAmount);
            Pot += Players[bbIndex].CurrentBet;
            CurrentBet = BigBlindAmount;
            AddLog($"💰 {Players[bbIndex].Name} Big Blind: {BigBlindAmount}");
        }

        private void SetFirstPlayerForPhase()
        {
            if (Phase == GamePhase.PreFlop)
            {
                // Preflop: start after BB
                int bbIndex = Players.Count == 2
                    ? (DealerIndex + 1) % Players.Count
                    : (DealerIndex + 2) % Players.Count;

                CurrentPlayerIndex = GetNextActivePlayer(bbIndex);
            }
            else
            {
                // Postflop: start after dealer
                CurrentPlayerIndex = GetNextActivePlayer(DealerIndex);
            }
        }

        // ====================================
        // Player Actions
        // ====================================
        public bool PerformAction(PlayerAction action, int raiseAmount = 0)
        {
            var player = CurrentPlayer;
            if (player == null || player.HasFolded) return false;

            switch (action)
            {
                case PlayerAction.Fold:
                    player.HasFolded = true;
                    player.LastAction = PlayerAction.Fold;
                    AddLog($"❌ {player.Name} fold yaptı");
                    break;

                case PlayerAction.Check:
                    if (CurrentBet > player.CurrentBet) return false; // Can't check if there's a bet
                    player.LastAction = PlayerAction.Check;
                    AddLog($"✋ {player.Name} check yaptı");
                    break;

                case PlayerAction.Call:
                    int callAmount = CurrentBet - player.CurrentBet;
                    if (callAmount <= 0)
                    {
                        player.LastAction = PlayerAction.Check;
                        AddLog($"✋ {player.Name} check yaptı");
                    }
                    else
                    {
                        int actualCall = Math.Min(callAmount, player.Chips);
                        player.PlaceBet(actualCall);
                        Pot += actualCall;
                        player.LastAction = player.IsAllIn ? PlayerAction.AllIn : PlayerAction.Call;
                        AddLog($"📞 {player.Name} call: {actualCall}" + (player.IsAllIn ? " (ALL-IN!)" : ""));
                    }
                    break;

                case PlayerAction.Raise:
                    int totalBet = raiseAmount;
                    int extraNeeded = totalBet - player.CurrentBet;
                    if (extraNeeded <= 0 || extraNeeded > player.Chips)
                    {
                        // All-in if can't afford the raise
                        extraNeeded = player.Chips;
                        totalBet = player.CurrentBet + extraNeeded;
                    }
                    player.PlaceBet(extraNeeded);
                    Pot += extraNeeded;
                    CurrentBet = player.CurrentBet;
                    player.LastAction = player.IsAllIn ? PlayerAction.AllIn : PlayerAction.Raise;
                    AddLog($"⬆️ {player.Name} raise: {totalBet}" + (player.IsAllIn ? " (ALL-IN!)" : ""));
                    break;

                case PlayerAction.AllIn:
                    int allInAmount = player.Chips;
                    player.PlaceBet(allInAmount);
                    Pot += allInAmount;
                    if (player.CurrentBet > CurrentBet)
                        CurrentBet = player.CurrentBet;
                    player.LastAction = PlayerAction.AllIn;
                    AddLog($"🔥 {player.Name} ALL-IN: {allInAmount}");
                    break;
            }

            // Check if hand is over (only 1 active player left)
            if (ActivePlayers.Count(p => !p.HasFolded) <= 1)
            {
                ResolveSingleWinner();
                return true;
            }

            // Move to next player or next phase
            AdvanceGame();
            return true;
        }

        private void AdvanceGame()
        {
            int nextPlayer = GetNextActivePlayer(CurrentPlayerIndex);

            // Check if betting round is complete
            bool roundComplete = IsRoundComplete();

            if (roundComplete)
            {
                MoveToNextPhase();
            }
            else
            {
                CurrentPlayerIndex = nextPlayer;

                // If the next player is a bot, trigger bot action
                if (CurrentPlayer?.IsBot == true && CurrentPlayer.IsActive)
                {
                    PerformBotAction(CurrentPlayer);
                }
            }
        }

        private bool IsRoundComplete()
        {
            var activePlayers = Players.Where(p => !p.HasFolded && !p.IsAllIn).ToList();

            if (activePlayers.Count == 0) return true;

            // All active players must have acted and have equal bets
            bool allEqualBets = activePlayers.All(p => p.CurrentBet == CurrentBet);
            bool allActed = activePlayers.All(p => p.LastAction != PlayerAction.None);

            return allEqualBets && allActed;
        }

        private void MoveToNextPhase()
        {
            // Reset bets for new round
            foreach (var player in Players)
            {
                player.CurrentBet = 0;
                player.LastAction = PlayerAction.None;
            }
            CurrentBet = 0;

            switch (Phase)
            {
                case GamePhase.PreFlop:
                    // Deal Flop (3 community cards)
                    Deck.Deal(); // Burn card
                    CommunityCards.AddRange(Deck.Deal(3));
                    Phase = GamePhase.Flop;
                    SaveDeckState();
                    AddLog($"🃏 FLOP: {string.Join(" ", CommunityCards.Select(c => c.DisplayName))}");
                    break;

                case GamePhase.Flop:
                    // Deal Turn (1 card)
                    Deck.Deal(); // Burn
                    CommunityCards.Add(Deck.Deal());
                    Phase = GamePhase.Turn;
                    SaveDeckState();
                    AddLog($"🃏 TURN: {CommunityCards.Last().DisplayName}");
                    break;

                case GamePhase.Turn:
                    // Deal River (1 card)
                    Deck.Deal(); // Burn
                    CommunityCards.Add(Deck.Deal());
                    Phase = GamePhase.River;
                    SaveDeckState();
                    AddLog($"🃏 RIVER: {CommunityCards.Last().DisplayName}");
                    break;

                case GamePhase.River:
                    // Showdown
                    Phase = GamePhase.Showdown;
                    ResolveShowdown();
                    return;
            }

            // Check if everyone is all-in — run out board
            var canAct = Players.Where(p => !p.HasFolded && !p.IsAllIn).ToList();
            if (canAct.Count <= 1)
            {
                // Run out remaining community cards
                while (Phase != GamePhase.Showdown && Phase != GamePhase.Finished)
                {
                    MoveToNextPhase();
                }
                return;
            }

            SetFirstPlayerForPhase();

            // Note: Bot actions will be triggered by RunPendingBotActions() or AdvanceGame()
        }

        // ====================================
        // Bot AI
        // ====================================
        private void PerformBotAction(PokerPlayer bot)
        {
            var strategy = PokerStrategy.Analyze(bot.HoleCards, CommunityCards, Phase, Pot, CurrentBet, bot.CurrentBet, bot.Chips, ActivePlayers.Count);

            // Decision based on hand strength and pot odds
            int callAmount = CurrentBet - bot.CurrentBet;
            double random = new Random().NextDouble();

            if (strategy.HandStrengthPercent >= 80)
            {
                // Strong hand: raise or all-in
                if (random < 0.7)
                {
                    int raiseSize = Math.Min(Pot + CurrentBet * 2, bot.Chips + bot.CurrentBet);
                    PerformAction(PlayerAction.Raise, raiseSize);
                }
                else
                {
                    PerformAction(PlayerAction.AllIn);
                }
            }
            else if (strategy.HandStrengthPercent >= 55)
            {
                // Medium hand: call or small raise
                if (callAmount == 0)
                {
                    if (random < 0.3)
                    {
                        int raiseSize = CurrentBet + BigBlindAmount * 2;
                        PerformAction(PlayerAction.Raise, raiseSize);
                    }
                    else
                    {
                        PerformAction(PlayerAction.Check);
                    }
                }
                else if (callAmount <= bot.Chips * 0.3)
                {
                    PerformAction(PlayerAction.Call);
                }
                else
                {
                    PerformAction(random < 0.3 ? PlayerAction.Call : PlayerAction.Fold);
                }
            }
            else if (strategy.HandStrengthPercent >= 35)
            {
                // Weak hand: check or fold
                if (callAmount == 0)
                {
                    PerformAction(PlayerAction.Check);
                }
                else if (callAmount <= BigBlindAmount * 2)
                {
                    PerformAction(random < 0.5 ? PlayerAction.Call : PlayerAction.Fold);
                }
                else
                {
                    // Bluff occasionally
                    PerformAction(random < 0.15 ? PlayerAction.Raise : PlayerAction.Fold);
                }
            }
            else
            {
                // Very weak: usually fold
                if (callAmount == 0)
                {
                    PerformAction(random < 0.1 ? PlayerAction.Raise : PlayerAction.Check);
                }
                else
                {
                    PerformAction(random < 0.08 ? PlayerAction.Call : PlayerAction.Fold);
                }
            }
        }

        // ====================================
        // Resolution
        // ====================================
        private void ResolveSingleWinner()
        {
            var winner = Players.First(p => !p.HasFolded);
            winner.Chips += Pot;
            WinnerMessage = $"🏆 {winner.Name} kazandı! (+{Pot} chip) — Diğerleri fold yaptı";
            AddLog(WinnerMessage);
            Phase = GamePhase.Finished;

            // Move dealer button
            DealerIndex = (DealerIndex + 1) % Players.Count;
        }

        private void ResolveShowdown()
        {
            AddLog("🎬 SHOWDOWN!");

            foreach (var player in ActivePlayers.Where(p => !p.HasFolded))
            {
                player.HandResult = HandEvaluator.Evaluate(player.HoleCards, CommunityCards);
                AddLog($"🃏 {player.Name}: {player.HoleCards[0].DisplayName} {player.HoleCards[1].DisplayName} → {player.HandResult.RankDisplayName}");
            }

            var contenders = ActivePlayers
                .Where(p => !p.HasFolded && p.HandResult != null)
                .OrderByDescending(p => p.HandResult!.Score)
                .ToList();

            if (contenders.Count > 0)
            {
                var winner = contenders[0];
                int winnerScore = winner.HandResult!.Score;

                // Check for tie
                var winners = contenders.Where(p => p.HandResult!.Score == winnerScore).ToList();

                if (winners.Count > 1)
                {
                    int share = Pot / winners.Count;
                    foreach (var w in winners)
                    {
                        w.Chips += share;
                    }
                    WinnerMessage = $"🤝 Berabere! {string.Join(", ", winners.Select(w => w.Name))} potu paylaştı ({share} chip her biri)";
                }
                else
                {
                    winner.Chips += Pot;
                    WinnerMessage = $"🏆 {winner.Name} kazandı! (+{Pot} chip) — {winner.HandResult.RankDisplayName}";
                }

                AddLog(WinnerMessage);
            }

            Phase = GamePhase.Finished;
            DealerIndex = (DealerIndex + 1) % Players.Count;
        }

        // ====================================
        // Helpers
        // ====================================
        private int GetNextActivePlayer(int from)
        {
            for (int i = 1; i <= Players.Count; i++)
            {
                int idx = (from + i) % Players.Count;
                if (!Players[idx].HasFolded && !Players[idx].IsAllIn)
                    return idx;
            }
            return from;
        }

        private void AddLog(string message)
        {
            ActionLog.Add(message);
            if (ActionLog.Count > 50)
                ActionLog.RemoveAt(0);
        }

        public bool CanCheck => CurrentBet <= (CurrentPlayer?.CurrentBet ?? 0);
        public int CallAmount => Math.Max(0, CurrentBet - (CurrentPlayer?.CurrentBet ?? 0));
        public int MinRaise => CurrentBet + BigBlindAmount;
        public bool IsHumanTurn => CurrentPlayer != null && !CurrentPlayer.IsBot && Phase != GamePhase.Finished && Phase != GamePhase.Showdown;
        public bool IsGameOver => Players.Count(p => p.Chips > 0) <= 1;
    }
}

namespace Blackjack.Models.Poker
{
    public class StrategyResult
    {
        public double ChenScore { get; set; }
        public double HandStrengthPercent { get; set; }
        public double PotOdds { get; set; }
        public int Outs { get; set; }
        public double DrawProbability { get; set; }
        public string RecommendedAction { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public string PositionAdvice { get; set; } = "";
        public string AggressionLevel { get; set; } = "";
        public List<string> Tips { get; set; } = new();
    }

    public static class PokerStrategy
    {
        // ====================================
        // Main Analysis
        // ====================================
        public static StrategyResult Analyze(
            List<PokerCard> holeCards,
            List<PokerCard> communityCards,
            GamePhase phase,
            int pot,
            int currentBet,
            int playerBet,
            int playerChips,
            int opponentCount)
        {
            var result = new StrategyResult();

            // Chen Score (preflop strength)
            if (holeCards.Count >= 2)
            {
                result.ChenScore = CalculateChenScore(holeCards[0], holeCards[1]);
            }

            // Hand strength
            if (communityCards.Count > 0)
            {
                var handResult = HandEvaluator.Evaluate(holeCards, communityCards);
                result.HandStrengthPercent = CalculateHandStrength(handResult, communityCards.Count);
            }
            else
            {
                result.HandStrengthPercent = ChenToStrength(result.ChenScore);
            }

            // Pot odds
            int callAmount = currentBet - playerBet;
            if (callAmount > 0 && pot > 0)
            {
                result.PotOdds = (double)callAmount / (pot + callAmount) * 100;
            }

            // Outs calculation
            if (communityCards.Count >= 3 && communityCards.Count < 5)
            {
                result.Outs = CalculateOuts(holeCards, communityCards);
                int cardsTocome = 5 - communityCards.Count;
                int remainingCards = 52 - holeCards.Count - communityCards.Count;
                result.DrawProbability = 1.0 - Math.Pow(
                    (double)(remainingCards - result.Outs) / remainingCards,
                    cardsTocome) * 100;
                if (result.DrawProbability < 0) result.DrawProbability = 0;
            }

            // Recommended action
            result.RecommendedAction = GetRecommendation(result, phase, callAmount, playerChips, pot, opponentCount);
            result.Reasoning = GetReasoning(result, phase, callAmount, pot);
            result.PositionAdvice = GetPositionAdvice(phase, opponentCount);
            result.AggressionLevel = GetAggressionLevel(result.HandStrengthPercent, phase);
            result.Tips = GetTips(result, phase);

            return result;
        }

        // ====================================
        // Chen Formula (Preflop Hand Strength)
        // ====================================
        public static double CalculateChenScore(PokerCard card1, PokerCard card2)
        {
            double score = 0;

            // Highest card value
            int high = Math.Max(card1.NumericRank, card2.NumericRank);
            int low = Math.Min(card1.NumericRank, card2.NumericRank);

            // Step 1: Score the highest card
            score = high switch
            {
                14 => 10,    // Ace
                13 => 8,     // King
                12 => 7,     // Queen
                11 => 6,     // Jack
                _ => high / 2.0
            };

            // Step 2: Pairs — double the score (minimum 5)
            if (card1.NumericRank == card2.NumericRank)
            {
                score = Math.Max(score * 2, 5);
                return Math.Ceiling(score);
            }

            // Step 3: Suited bonus
            bool suited = card1.Suit == card2.Suit;
            if (suited) score += 2;

            // Step 4: Gap penalty
            int gap = high - low - 1;
            score -= gap switch
            {
                0 => 0,     // Connected
                1 => -1,    // One gap
                2 => -2,    // Two gap
                3 => -4,    // Three gap
                _ => -5     // Four+ gap
            };

            // Step 5: Straight bonus for low connected cards
            if (gap <= 1 && low <= 12) // Both cards can make a straight
            {
                score += 1;
            }

            return Math.Max(Math.Ceiling(score), 0);
        }

        // ====================================
        // Hand Strength Percent
        // ====================================
        private static double CalculateHandStrength(HandResult hand, int communityCount)
        {
            return hand.Rank switch
            {
                HandRank.RoyalFlush => 100,
                HandRank.StraightFlush => 97,
                HandRank.FourOfAKind => 93,
                HandRank.FullHouse => 85,
                HandRank.Flush => 78,
                HandRank.Straight => 72,
                HandRank.ThreeOfAKind => 60,
                HandRank.TwoPair => 48,
                HandRank.OnePair => 32 + (hand.Kickers.Count > 0 ? hand.Kickers[0] : 0),
                HandRank.HighCard => 15 + (hand.Kickers.Count > 0 ? hand.Kickers[0] : 0),
                _ => 10
            };
        }

        private static double ChenToStrength(double chen)
        {
            // Convert Chen score (0-20) to strength percentage
            if (chen >= 12) return 85;  // Premium hands (AA, KK, QQ, AKs)
            if (chen >= 10) return 75;  // Strong hands
            if (chen >= 8) return 60;   // Good hands
            if (chen >= 6) return 45;   // Playable
            if (chen >= 4) return 30;   // Marginal
            return 15;                   // Weak
        }

        // ====================================
        // Outs Calculation
        // ====================================
        private static int CalculateOuts(List<PokerCard> holeCards, List<PokerCard> communityCards)
        {
            int outs = 0;
            var allKnown = new List<PokerCard>();
            allKnown.AddRange(holeCards);
            allKnown.AddRange(communityCards);

            var allCards = GenerateAllCards();
            var unknownCards = allCards.Where(c => !allKnown.Any(k => k.Suit == c.Suit && k.Rank == c.Rank)).ToList();

            var currentResult = HandEvaluator.Evaluate(holeCards, communityCards);

            foreach (var card in unknownCards)
            {
                var testCommunity = new List<PokerCard>(communityCards) { card };
                var testResult = HandEvaluator.Evaluate(holeCards, testCommunity);
                if (testResult.Score > currentResult.Score)
                {
                    outs++;
                }
            }

            return outs;
        }

        private static List<PokerCard> GenerateAllCards()
        {
            var cards = new List<PokerCard>();
            string[] suits = { "Kupa", "Karo", "Sinek", "Maça" };
            string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };

            foreach (var suit in suits)
                foreach (var rank in ranks)
                    cards.Add(new PokerCard { Suit = suit, Rank = rank });

            return cards;
        }

        // ====================================
        // Recommendation Engine
        // ====================================
        private static string GetRecommendation(StrategyResult result, GamePhase phase, int callAmount, int chips, int pot, int opponents)
        {
            double strength = result.HandStrengthPercent;

            if (callAmount == 0) // No bet to call
            {
                if (strength >= 80) return "⬆️ RAISE — Güçlü eliniz var, değer artırmak için raise yapın!";
                if (strength >= 50) return "⬆️ RAISE veya ✋ CHECK — Pozisyonunuza göre karar verin";
                if (strength >= 30) return "✋ CHECK — Bedava kart görün";
                return "✋ CHECK — Eliniz zayıf, risk almayın";
            }
            else // There's a bet
            {
                double potOdds = result.PotOdds;
                double equity = strength;

                if (strength >= 85) return "⬆️ RE-RAISE — Çok güçlü el, agresif oynayın!";
                if (strength >= 70) return "📞 CALL veya ⬆️ RAISE — Güçlü eliniz var";
                if (strength >= 50 && potOdds < 30) return "📞 CALL — Pot odds iyi, çağırın";
                if (result.Outs > 8 && potOdds < 40) return "📞 CALL — Draw olasılığınız iyi";
                if (callAmount > chips * 0.3 && strength < 60) return "❌ FOLD — Riskli, chip korunumunu düşünün";
                if (strength >= 40) return "📞 CALL veya ❌ FOLD — Riske toleransınıza bağlı";
                return "❌ FOLD — Eliniz zayıf, chip koruyun";
            }
        }

        private static string GetReasoning(StrategyResult result, GamePhase phase, int callAmount, int pot)
        {
            var reasons = new List<string>();

            if (phase == GamePhase.PreFlop)
            {
                if (result.ChenScore >= 10)
                    reasons.Add($"Chen skoru {result.ChenScore:F0} — Premium başlangıç eli");
                else if (result.ChenScore >= 7)
                    reasons.Add($"Chen skoru {result.ChenScore:F0} — Oynanabilir el");
                else
                    reasons.Add($"Chen skoru {result.ChenScore:F0} — Zayıf başlangıç eli");
            }

            reasons.Add($"El gücü: %{result.HandStrengthPercent:F0}");

            if (result.PotOdds > 0)
                reasons.Add($"Pot odds: %{result.PotOdds:F1}");

            if (result.Outs > 0)
                reasons.Add($"Outs: {result.Outs} kart ({result.DrawProbability:F1}% şans)");

            return string.Join(" | ", reasons);
        }

        private static string GetPositionAdvice(GamePhase phase, int opponentCount)
        {
            if (phase == GamePhase.PreFlop)
            {
                if (opponentCount <= 2) return "🎯 Az rakip — Daha agresif oynayabilirsiniz";
                if (opponentCount >= 5) return "🛡️ Çok rakip — Daha seçici olun";
                return "⚖️ Orta düzey rekabet — Standard strateji uygulayın";
            }
            return "";
        }

        private static string GetAggressionLevel(double strength, GamePhase phase)
        {
            if (strength >= 80) return "🔥 Çok Agresif";
            if (strength >= 60) return "⚡ Agresif";
            if (strength >= 40) return "⚖️ Dengeli";
            if (strength >= 25) return "🛡️ Defansif";
            return "❄️ Pasif";
        }

        private static List<string> GetTips(StrategyResult result, GamePhase phase)
        {
            var tips = new List<string>();

            if (phase == GamePhase.PreFlop)
            {
                if (result.ChenScore >= 12) tips.Add("💡 Premium el! Raise için ideal pozisyon");
                if (result.ChenScore < 5) tips.Add("💡 Weak el — Fold en güvenli seçenek");
            }

            if (result.Outs >= 9) tips.Add($"💡 {result.Outs} outs ile güçlü draw var!");
            if (result.Outs >= 4 && result.Outs < 9) tips.Add($"💡 {result.Outs} outs — Pot odds'a bakarak karar verin");

            if (result.PotOdds > 0 && result.PotOdds < 20) tips.Add("💡 Pot odds iyi — Call düşünülebilir");
            if (result.PotOdds > 40) tips.Add("💡 Pot odds yüksek — Dikkatli olun");

            if (result.HandStrengthPercent >= 85) tips.Add("🏆 Çok güçlü el — Slow play veya value bet yapabilirsiniz");

            return tips;
        }

        // ====================================
        // Format for AI Prompt
        // ====================================
        public static string FormatForAI(StrategyResult strategy, List<PokerCard> holeCards, List<PokerCard> communityCards, GamePhase phase, int pot, int chips)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== POKER OYUN DURUMU ===");
            sb.AppendLine($"Fase: {phase}");
            sb.AppendLine($"El Kartları: {string.Join(" ", holeCards.Select(c => c.DisplayName))}");

            if (communityCards.Count > 0)
                sb.AppendLine($"Community: {string.Join(" ", communityCards.Select(c => c.DisplayName))}");

            sb.AppendLine($"Pot: {pot} chip");
            sb.AppendLine($"Chip: {chips}");
            sb.AppendLine();
            sb.AppendLine("=== STRATEJİ ANALİZİ ===");
            sb.AppendLine($"Chen Skoru: {strategy.ChenScore:F0}/20");
            sb.AppendLine($"El Gücü: %{strategy.HandStrengthPercent:F0}");

            if (strategy.PotOdds > 0) sb.AppendLine($"Pot Odds: %{strategy.PotOdds:F1}");
            if (strategy.Outs > 0) sb.AppendLine($"Outs: {strategy.Outs} ({strategy.DrawProbability:F1}% şans)");

            sb.AppendLine($"Öneri: {strategy.RecommendedAction}");
            sb.AppendLine($"Agresyon: {strategy.AggressionLevel}");

            return sb.ToString();
        }
    }
}

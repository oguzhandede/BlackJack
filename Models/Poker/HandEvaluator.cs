namespace Blackjack.Models.Poker
{
    public class HandResult
    {
        public HandRank Rank { get; set; }
        public List<PokerCard> BestHand { get; set; } = new();
        public List<int> Kickers { get; set; } = new();
        public string Description { get; set; } = "";
        public int Score { get; set; }

        public string RankDisplayName => Rank switch
        {
            HandRank.RoyalFlush => "🏆 Royal Flush",
            HandRank.StraightFlush => "⭐ Straight Flush",
            HandRank.FourOfAKind => "💎 Dörtlü (Four of a Kind)",
            HandRank.FullHouse => "🏠 Full House",
            HandRank.Flush => "🌊 Flush",
            HandRank.Straight => "📏 Straight",
            HandRank.ThreeOfAKind => "🔺 Üçlü (Three of a Kind)",
            HandRank.TwoPair => "✌️ İki Çift (Two Pair)",
            HandRank.OnePair => "👆 Bir Çift (One Pair)",
            HandRank.HighCard => "🃏 Yüksek Kart (High Card)",
            _ => "?"
        };
    }

    public static class HandEvaluator
    {
        public static HandResult Evaluate(List<PokerCard> holeCards, List<PokerCard> communityCards)
        {
            var allCards = new List<PokerCard>();
            allCards.AddRange(holeCards);
            allCards.AddRange(communityCards);

            if (allCards.Count < 5)
            {
                return EvaluateBestFromCards(allCards);
            }

            // Try all 5-card combinations from available cards
            var combinations = GetCombinations(allCards, 5);
            HandResult? best = null;

            foreach (var combo in combinations)
            {
                var result = EvaluateFiveCards(combo);
                if (best == null || CompareHands(result, best) > 0)
                {
                    best = result;
                }
            }

            return best ?? new HandResult { Rank = HandRank.HighCard, Description = "Değerlendirilemiyor" };
        }

        private static HandResult EvaluateBestFromCards(List<PokerCard> cards)
        {
            if (cards.Count == 0)
                return new HandResult { Rank = HandRank.HighCard, Description = "Kart yok" };

            var sorted = cards.OrderByDescending(c => c.NumericRank).ToList();
            return new HandResult
            {
                Rank = HandRank.HighCard,
                BestHand = sorted,
                Kickers = sorted.Select(c => c.NumericRank).ToList(),
                Description = $"Yüksek Kart: {sorted[0].Rank}",
                Score = CalculateScore(HandRank.HighCard, sorted.Select(c => c.NumericRank).ToList())
            };
        }

        private static HandResult EvaluateFiveCards(List<PokerCard> cards)
        {
            var sorted = cards.OrderByDescending(c => c.NumericRank).ToList();
            var ranks = sorted.Select(c => c.NumericRank).ToList();
            var suits = sorted.Select(c => c.Suit).ToList();

            bool isFlush = suits.Distinct().Count() == 1;
            bool isStraight = IsStraight(ranks, out int highCard);

            // Royal Flush
            if (isFlush && isStraight && highCard == 14)
            {
                return new HandResult
                {
                    Rank = HandRank.RoyalFlush,
                    BestHand = sorted,
                    Description = $"Royal Flush! ({sorted[0].Suit})",
                    Score = CalculateScore(HandRank.RoyalFlush, new List<int> { 14 })
                };
            }

            // Straight Flush
            if (isFlush && isStraight)
            {
                return new HandResult
                {
                    Rank = HandRank.StraightFlush,
                    BestHand = sorted,
                    Description = $"Straight Flush! {highCard} yüksek ({sorted[0].Suit})",
                    Score = CalculateScore(HandRank.StraightFlush, new List<int> { highCard })
                };
            }

            var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();

            // Four of a Kind
            if (groups[0].Count() == 4)
            {
                int quadRank = groups[0].Key;
                int kicker = groups[1].Key;
                return new HandResult
                {
                    Rank = HandRank.FourOfAKind,
                    BestHand = sorted,
                    Kickers = new List<int> { quadRank, kicker },
                    Description = $"Dörtlü: {GetRankName(quadRank)}",
                    Score = CalculateScore(HandRank.FourOfAKind, new List<int> { quadRank, kicker })
                };
            }

            // Full House
            if (groups[0].Count() == 3 && groups[1].Count() == 2)
            {
                return new HandResult
                {
                    Rank = HandRank.FullHouse,
                    BestHand = sorted,
                    Kickers = new List<int> { groups[0].Key, groups[1].Key },
                    Description = $"Full House: {GetRankName(groups[0].Key)} üçlü, {GetRankName(groups[1].Key)} çift",
                    Score = CalculateScore(HandRank.FullHouse, new List<int> { groups[0].Key, groups[1].Key })
                };
            }

            // Flush
            if (isFlush)
            {
                return new HandResult
                {
                    Rank = HandRank.Flush,
                    BestHand = sorted,
                    Kickers = ranks,
                    Description = $"Flush: {sorted[0].Suit}",
                    Score = CalculateScore(HandRank.Flush, ranks)
                };
            }

            // Straight
            if (isStraight)
            {
                return new HandResult
                {
                    Rank = HandRank.Straight,
                    BestHand = sorted,
                    Kickers = new List<int> { highCard },
                    Description = $"Straight: {GetRankName(highCard)} yüksek",
                    Score = CalculateScore(HandRank.Straight, new List<int> { highCard })
                };
            }

            // Three of a Kind
            if (groups[0].Count() == 3)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).Take(2).ToList();
                return new HandResult
                {
                    Rank = HandRank.ThreeOfAKind,
                    BestHand = sorted,
                    Kickers = new List<int> { groups[0].Key }.Concat(kickers).ToList(),
                    Description = $"Üçlü: {GetRankName(groups[0].Key)}",
                    Score = CalculateScore(HandRank.ThreeOfAKind, new List<int> { groups[0].Key }.Concat(kickers).ToList())
                };
            }

            // Two Pair
            if (groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                int highPair = Math.Max(groups[0].Key, groups[1].Key);
                int lowPair = Math.Min(groups[0].Key, groups[1].Key);
                int kicker = groups[2].Key;
                return new HandResult
                {
                    Rank = HandRank.TwoPair,
                    BestHand = sorted,
                    Kickers = new List<int> { highPair, lowPair, kicker },
                    Description = $"İki Çift: {GetRankName(highPair)} ve {GetRankName(lowPair)}",
                    Score = CalculateScore(HandRank.TwoPair, new List<int> { highPair, lowPair, kicker })
                };
            }

            // One Pair
            if (groups[0].Count() == 2)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).Take(3).ToList();
                return new HandResult
                {
                    Rank = HandRank.OnePair,
                    BestHand = sorted,
                    Kickers = new List<int> { groups[0].Key }.Concat(kickers).ToList(),
                    Description = $"Bir Çift: {GetRankName(groups[0].Key)}",
                    Score = CalculateScore(HandRank.OnePair, new List<int> { groups[0].Key }.Concat(kickers).ToList())
                };
            }

            // High Card
            return new HandResult
            {
                Rank = HandRank.HighCard,
                BestHand = sorted,
                Kickers = ranks,
                Description = $"Yüksek Kart: {GetRankName(ranks[0])}",
                Score = CalculateScore(HandRank.HighCard, ranks)
            };
        }

        private static bool IsStraight(List<int> ranks, out int highCard)
        {
            var distinct = ranks.Distinct().OrderByDescending(r => r).ToList();
            highCard = distinct[0];

            if (distinct.Count < 5) return false;

            // Normal straight check
            if (distinct[0] - distinct[4] == 4 && distinct.Count == 5)
                return true;

            // Ace-low straight (A-2-3-4-5 = wheel)
            if (distinct.Contains(14) && distinct.Contains(2) && distinct.Contains(3) && distinct.Contains(4) && distinct.Contains(5))
            {
                highCard = 5;
                return true;
            }

            return false;
        }

        public static int CompareHands(HandResult a, HandResult b)
        {
            if (a.Score != b.Score) return a.Score.CompareTo(b.Score);
            return 0;
        }

        private static int CalculateScore(HandRank rank, List<int> kickers)
        {
            // Base score from hand rank (each rank has huge gap)
            int score = (int)rank * 1_000_000;

            // Add kicker values with decreasing significance
            int multiplier = 10000;
            foreach (var k in kickers.Take(5))
            {
                score += k * multiplier;
                multiplier /= 15;
            }

            return score;
        }

        private static string GetRankName(int numericRank) => numericRank switch
        {
            14 => "As",
            13 => "Papaz",
            12 => "Kız",
            11 => "Vale",
            _ => numericRank.ToString()
        };

        private static List<List<PokerCard>> GetCombinations(List<PokerCard> cards, int k)
        {
            var result = new List<List<PokerCard>>();
            GetCombinationsHelper(cards, k, 0, new List<PokerCard>(), result);
            return result;
        }

        private static void GetCombinationsHelper(List<PokerCard> cards, int k, int start, List<PokerCard> current, List<List<PokerCard>> result)
        {
            if (current.Count == k)
            {
                result.Add(new List<PokerCard>(current));
                return;
            }

            for (int i = start; i < cards.Count; i++)
            {
                current.Add(cards[i]);
                GetCombinationsHelper(cards, k, i + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }
    }
}

using System.Text.Json.Serialization;

namespace Blackjack.Models
{
    public class BlackjackGame
    {
        public Deck Deck { get; set; }
        public List<Card> RemovedCards { get; set; }

        [JsonConstructor]
        public BlackjackGame()
        {
            Deck = new Deck();
            RemovedCards = new List<Card>();
        }

        public BlackjackGame(int numberOfDecks)
        {
            Deck = new Deck(numberOfDecks);
            RemovedCards = new List<Card>();
        }

        public void RemoveCard(string suit, string rank)
        {
            var card = Deck.Cards.FirstOrDefault(c => c.Suit == suit && c.Rank == rank);
            if (card != null)
            {
                Deck.Cards.Remove(card);
                RemovedCards.Add(card);
            }
        }

        public void AddCardBack(string suit, string rank)
        {
            var card = RemovedCards.FirstOrDefault(c => c.Suit == suit && c.Rank == rank);
            if (card != null)
            {
                RemovedCards.Remove(card);
                Deck.Cards.Add(card);
            }
        }

        public Dictionary<int, double> CalculateProbabilities()
        {
            var probabilities = new Dictionary<int, double>();
            int totalCards = Deck.Cards.Count;

            if (totalCards == 0) return probabilities;

            var valueGroups = Deck.Cards.GroupBy(c => c.Value)
                .Select(g => new { Value = g.Key, Count = g.Count() });

            foreach (var group in valueGroups)
            {
                double probability = (double)group.Count / totalCards * 100;
                probabilities[group.Value] = probability;
            }

            return probabilities;
        }

        public int GetRunningCount()
        {
            // Hi-Lo card counting system
            int count = 0;
            foreach (var card in RemovedCards)
            {
                count += card.Value switch
                {
                    2 or 3 or 4 or 5 or 6 => 1,
                    7 or 8 or 9 => 0,
                    10 or 11 => -1,
                    _ => 0
                };
            }
            return count;
        }

        public double GetTrueCount(int numberOfDecks)
        {
            int remainingCards = Deck.Cards.Count;
            double remainingDecks = (double)remainingCards / 52;
            if (remainingDecks < 0.25) remainingDecks = 0.25;
            return GetRunningCount() / remainingDecks;
        }
    }
}
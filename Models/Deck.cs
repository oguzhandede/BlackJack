using System.Text.Json.Serialization;

namespace Blackjack.Models
{
    public class Deck
    {
        public List<Card> Cards { get; set; }

        [JsonConstructor]
        public Deck()
        {
            Cards = new List<Card>();
        }

        public Deck(int numberOfDecks)
        {
            Cards = new List<Card>();
            string[] suits = { "Kupa", "Karo", "Sinek", "Maça" };
            string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };

            for (int i = 0; i < numberOfDecks; i++)
            {
                foreach (var suit in suits)
                {
                    foreach (var rank in ranks)
                    {
                        Cards.Add(new Card { Suit = suit, Rank = rank });
                    }
                }
            }
        }

        public bool CardExists(string suit, string rank)
        {
            return Cards.Any(c => c.Suit == suit && c.Rank == rank);
        }
    }
}
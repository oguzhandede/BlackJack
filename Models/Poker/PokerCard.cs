using System.Text.Json.Serialization;

namespace Blackjack.Models.Poker
{
    public class PokerCard : IComparable<PokerCard>
    {
        public string Suit { get; set; } = "";
        public string Rank { get; set; } = "";

        [JsonIgnore]
        public int NumericRank => Rank switch
        {
            "2" => 2, "3" => 3, "4" => 4, "5" => 5,
            "6" => 6, "7" => 7, "8" => 8, "9" => 9,
            "10" => 10, "Vale" => 11, "Kız" => 12,
            "Papaz" => 13, "As" => 14,
            _ => 0
        };

        [JsonIgnore]
        public string SuitSymbol => Suit switch
        {
            "Kupa" => "♥", "Karo" => "♦",
            "Sinek" => "♣", "Maça" => "♠",
            _ => "?"
        };

        [JsonIgnore]
        public string SuitClass => Suit switch
        {
            "Kupa" => "kupa", "Karo" => "karo",
            "Sinek" => "sinek", "Maça" => "maca",
            _ => ""
        };

        [JsonIgnore]
        public bool IsRed => Suit == "Kupa" || Suit == "Karo";

        public string DisplayName => $"{SuitSymbol}{Rank}";

        public int CompareTo(PokerCard? other)
        {
            if (other == null) return 1;
            return NumericRank.CompareTo(other.NumericRank);
        }

        public override string ToString() => $"{Rank} {Suit}";

        public override bool Equals(object? obj)
        {
            if (obj is PokerCard other)
                return Suit == other.Suit && Rank == other.Rank;
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(Suit, Rank);
    }
}

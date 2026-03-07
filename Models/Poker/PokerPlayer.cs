using System.Text.Json.Serialization;

namespace Blackjack.Models.Poker
{
    public class PokerPlayer
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsBot { get; set; }
        public bool IsDealer { get; set; }
        public int Chips { get; set; }
        public int CurrentBet { get; set; }
        public int TotalBetThisRound { get; set; }
        public List<PokerCard> HoleCards { get; set; } = new();
        public PlayerAction LastAction { get; set; } = PlayerAction.None;
        public bool HasFolded { get; set; }
        public bool IsAllIn { get; set; }
        public bool IsActive => !HasFolded && Chips >= 0;
        public PlayerPosition Position { get; set; }

        [JsonIgnore]
        public HandResult? HandResult { get; set; }

        public string PositionDisplay => Position switch
        {
            PlayerPosition.SmallBlind => "SB",
            PlayerPosition.BigBlind => "BB",
            PlayerPosition.UTG => "UTG",
            PlayerPosition.Middle => "MP",
            PlayerPosition.CutOff => "CO",
            PlayerPosition.Button => "BTN",
            _ => ""
        };

        public void Reset()
        {
            HoleCards.Clear();
            CurrentBet = 0;
            TotalBetThisRound = 0;
            LastAction = PlayerAction.None;
            HasFolded = false;
            IsAllIn = false;
            HandResult = null;
        }

        public void PlaceBet(int amount)
        {
            int actualBet = Math.Min(amount, Chips);
            Chips -= actualBet;
            CurrentBet += actualBet;
            TotalBetThisRound += actualBet;
            if (Chips == 0) IsAllIn = true;
        }
    }
}

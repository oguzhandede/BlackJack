namespace Blackjack.Models.Poker
{
    public enum HandRank
    {
        HighCard = 0,
        OnePair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        FourOfAKind = 7,
        StraightFlush = 8,
        RoyalFlush = 9
    }

    public enum GamePhase
    {
        Lobby,
        PreFlop,
        Flop,
        Turn,
        River,
        Showdown,
        Finished
    }

    public enum PlayerAction
    {
        None,
        Fold,
        Check,
        Call,
        Raise,
        AllIn
    }

    public enum PlayerPosition
    {
        SmallBlind,
        BigBlind,
        UTG,        // Under The Gun
        Middle,
        CutOff,
        Button      // Dealer
    }
}

namespace Blackjack.Models.Poker
{
    public class PokerDeck
    {
        private static readonly string[] Suits = { "Kupa", "Karo", "Sinek", "Maça" };
        private static readonly string[] Ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };

        private readonly List<PokerCard> _cards = new();
        private readonly Random _random = new();
        private int _dealIndex;

        public PokerDeck()
        {
            Reset();
        }

        public void Reset()
        {
            _cards.Clear();
            foreach (var suit in Suits)
            {
                foreach (var rank in Ranks)
                {
                    _cards.Add(new PokerCard { Suit = suit, Rank = rank });
                }
            }
            Shuffle();
        }

        public void Shuffle()
        {
            _dealIndex = 0;
            // Fisher-Yates shuffle
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public PokerCard Deal()
        {
            if (_dealIndex >= _cards.Count)
                throw new InvalidOperationException("Destede kart kalmadı!");
            return _cards[_dealIndex++];
        }

        public List<PokerCard> Deal(int count)
        {
            var hand = new List<PokerCard>();
            for (int i = 0; i < count; i++)
                hand.Add(Deal());
            return hand;
        }

        public int RemainingCards => _cards.Count - _dealIndex;

        // State persistence methods for session serialization
        public void LoadState(List<PokerCard> cards, int dealIndex)
        {
            _cards.Clear();
            _cards.AddRange(cards);
            _dealIndex = dealIndex;
        }

        public List<PokerCard> GetCards() => new List<PokerCard>(_cards);

        public int GetDealIndex() => _dealIndex;
    }
}

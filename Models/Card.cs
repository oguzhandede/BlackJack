namespace Blackjack.Models
{
    public class Card
    {
        public string Suit { get; set; } = "";
        public string Rank { get; set; } = "";
        public int Value
        {
            get
            {
                return Rank switch
                {
                    "2" => 2,
                    "3" => 3,
                    "4" => 4,
                    "5" => 5,
                    "6" => 6,
                    "7" => 7,
                    "8" => 8,
                    "9" => 9,
                    "10" => 10,
                    "Vale" => 10,
                    "Kız" => 10,
                    "Papaz" => 10,
                    "As" => 11,
                    _ => 0
                };
            }
        }
    }
}

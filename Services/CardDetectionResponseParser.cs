using System.Text.Json;

namespace Blackjack.Services;

internal static class CardDetectionResponseParser
{
    private static readonly Dictionary<string, string> SuitMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kupa"] = "Kupa",
        ["Karo"] = "Karo",
        ["Sinek"] = "Sinek",
        ["Maça"] = "Maça",
        ["Maca"] = "Maça"
    };

    private static readonly Dictionary<string, string> RankMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["2"] = "2",
        ["3"] = "3",
        ["4"] = "4",
        ["5"] = "5",
        ["6"] = "6",
        ["7"] = "7",
        ["8"] = "8",
        ["9"] = "9",
        ["10"] = "10",
        ["Vale"] = "Vale",
        ["Kız"] = "Kız",
        ["Kiz"] = "Kız",
        ["Papaz"] = "Papaz",
        ["As"] = "As"
    };

    public static CardDetectionResult Parse(string aiResponse, ILogger logger)
    {
        try
        {
            var cleaned = aiResponse.Trim();
            if (cleaned.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = cleaned.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    cleaned = cleaned[(firstNewLine + 1)..];
                }

                var lastBacktick = cleaned.LastIndexOf("```", StringComparison.Ordinal);
                if (lastBacktick >= 0)
                {
                    cleaned = cleaned[..lastBacktick];
                }

                cleaned = cleaned.Trim();
            }

            var parsed = JsonSerializer.Deserialize<CardDetectionJson>(cleaned, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed?.Cards == null || parsed.Cards.Count == 0)
            {
                return new CardDetectionResult
                {
                    Success = true,
                    Cards = new List<DetectedCard>(),
                    Message = "Görüntüde kart tespit edilemedi."
                };
            }

            var validCards = parsed.Cards
                .Select(MapCard)
                .Where(card => card != null)
                .Cast<DetectedCard>()
                .ToList();

            return new CardDetectionResult
            {
                Success = true,
                Cards = validCards,
                Message = validCards.Count > 0
                    ? $"{validCards.Count} kart algılandı!"
                    : "Geçerli kart tespit edilemedi."
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse card detection response: {Response}", aiResponse);
            return new CardDetectionResult
            {
                Success = true,
                Cards = new List<DetectedCard>(),
                Message = "Kart algılama yanıtı okunamadı. Tekrar deneyin."
            };
        }
    }

    private static DetectedCard? MapCard(CardJson? card)
    {
        if (card == null)
        {
            return null;
        }

        var suit = Normalize(SuitMap, card.Suit);
        var rank = Normalize(RankMap, card.Rank);

        if (suit == null || rank == null)
        {
            return null;
        }

        return new DetectedCard
        {
            Suit = suit,
            Rank = rank
        };
    }

    private static string? Normalize(IReadOnlyDictionary<string, string> map, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return map.TryGetValue(value.Trim(), out var normalized)
            ? normalized
            : null;
    }
}

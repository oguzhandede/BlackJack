using System.Text.Json.Serialization;

namespace Blackjack.Services;

public class CardDetectionResult
{
    public bool Success { get; set; }
    public List<DetectedCard> Cards { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class DetectedCard
{
    public string Suit { get; set; } = "";
    public string Rank { get; set; } = "";
}

public class CardDetectionJson
{
    [JsonPropertyName("cards")]
    public List<CardJson>? Cards { get; set; }
}

public class CardJson
{
    [JsonPropertyName("suit")]
    public string? Suit { get; set; }

    [JsonPropertyName("rank")]
    public string? Rank { get; set; }
}

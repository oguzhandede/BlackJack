using System.Text;
using System.Text.Json;

namespace Blackjack.Services;

internal static class AIMessageFactory
{
    public static string BuildChatSystemPrompt(object? gameState)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen profesyonel bir Blackjack strateji danışmanısın. Türkçe yanıt ver.");
        sb.AppendLine("Görevin:");
        sb.AppendLine("1. Kart sayma teknikleri (Hi-Lo sistemi) konusunda analiz yapmak");
        sb.AppendLine("2. Oyuncuya stratejik öneriler sunmak (Hit, Stand, Double Down, Split)");
        sb.AppendLine("3. Olasılık ve risk analizi yapmak");
        sb.AppendLine("4. Blackjack kuralları ve taktikleri hakkında eğitici bilgi vermek");
        sb.AppendLine();
        sb.AppendLine("ÖNEMLİ: Bu tamamen eğitim amaçlıdır. Kumar teşvik etme.");
        sb.AppendLine("Yanıtlarını kısa ve öz tut. Emojiler kullan. Markdown formatı kullanma.");

        if (gameState != null)
        {
            sb.AppendLine();
            sb.AppendLine("MEVCUT OYUN DURUMU:");
            sb.AppendLine(JsonSerializer.Serialize(gameState, new JsonSerializerOptions { WriteIndented = true }));
        }

        return sb.ToString();
    }

    public static string BuildPokerSystemPrompt(object? gameState)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen profesyonel bir Texas Hold'em Poker strateji danışmanısın. Türkçe yanıt ver.");
        sb.AppendLine("Görevin:");
        sb.AppendLine("1. Oyuncunun elini değerlendirmek (Chen Formula, el gücü analizi)");
        sb.AppendLine("2. Pot odds ve outs hesaplaması yapmak");
        sb.AppendLine("3. Fold, Check, Call, Raise veya All-In kararı önermek");
        sb.AppendLine("4. GTO (Game Theory Optimal) stratejiler hakkında eğitici bilgi vermek");
        sb.AppendLine("5. Rakip davranışlarını analiz etmek");
        sb.AppendLine("6. Pozisyon avantajını değerlendirmek (Button, SB, BB, UTG vb.)");
        sb.AppendLine("7. Blöf zamanlaması ve value bet kavramlarını açıklamak");
        sb.AppendLine();
        sb.AppendLine("ÖNEMLİ: Bu tamamen eğitim amaçlıdır. Kumar teşvik etme.");
        sb.AppendLine("Yanıtlarını kısa ve öz tut. Emojiler kullan. Markdown formatı kullanma.");
        sb.AppendLine("Mümkünse somut sayısal analizler sun (ör. pot odds %25, equity %35 gibi).");

        if (gameState != null)
        {
            sb.AppendLine();
            sb.AppendLine("MEVCUT POKER OYUN DURUMU:");
            sb.AppendLine(JsonSerializer.Serialize(gameState, new JsonSerializerOptions { WriteIndented = true }));
        }

        return sb.ToString();
    }

    public static string BuildVisionSystemPrompt()
    {
        return @"Bu görüntüde Blackjack oyun kartları var. Lütfen görüntüdeki TÜM oyun kartlarını tespit et.

KURALLAR:
- Sadece net olarak görünen kartları bildir
- Emin olmadığın kartları bildirme
- Türkçe takım isimleri kullan: Kupa, Karo, Sinek, Maça
- Türkçe kart isimleri kullan: 2-10 arası sayılar, Vale, Kız, Papaz, As

YANIT FORMATI — SADECE aşağıdaki JSON formatında yanıt ver, başka hiçbir şey yazma:
{""cards"": [{""suit"": ""Kupa"", ""rank"": ""As""}, {""suit"": ""Maça"", ""rank"": ""10""}]}

Eğer hiç kart tespit edemediysen:
{""cards"": []}

SADECE JSON DÖNDÜR, başka açıklama yazma.";
    }
}

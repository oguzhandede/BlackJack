using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blackjack.Services
{
    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenRouterService> _logger;

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var baseUrl = _configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";
            _httpClient.BaseAddress = new Uri(baseUrl);

            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://blackjack-ai.local");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Blackjack AI Strategy Advisor");
        }

        // =============================================
        // Chat Response (Text only)
        // =============================================
        public async Task<string> GetChatResponseAsync(string userMessage, object? gameState = null)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return "⚠️ OpenRouter API key yapılandırılmamış. Lütfen appsettings.json dosyasına API key ekleyin.";
            }

            var model = _configuration["OpenRouter:Model"] ?? "google/gemini-2.0-flash-001";
            var systemPrompt = BuildChatSystemPrompt(gameState);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = (object)systemPrompt },
                    new { role = "user", content = (object)userMessage }
                },
                max_tokens = 800,
                temperature = 0.7
            };

            return await CallOpenRouterAsync(requestBody);
        }

        // =============================================
        // Poker Chat Response
        // =============================================
        public async Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return "⚠️ OpenRouter API key yapılandırılmamış. Lütfen appsettings.json dosyasına API key ekleyin.";
            }

            var model = _configuration["OpenRouter:Model"] ?? "google/gemini-2.0-flash-001";
            var systemPrompt = BuildPokerSystemPrompt(gameState);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = (object)systemPrompt },
                    new { role = "user", content = (object)userMessage }
                },
                max_tokens = 800,
                temperature = 0.7
            };

            return await CallOpenRouterAsync(requestBody);
        }

        // =============================================
        // Vision-based Card Detection
        // =============================================
        public async Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return new CardDetectionResult
                {
                    Success = false,
                    Error = "API key yapılandırılmamış."
                };
            }

            var visionModel = _configuration["OpenRouter:VisionModel"]
                ?? _configuration["OpenRouter:Model"]
                ?? "google/gemini-2.0-flash-001";

            // Determine image MIME type from base64 header or default to png
            string mimeType = "image/png";
            string imageData = base64Image;

            if (base64Image.StartsWith("data:"))
            {
                var parts = base64Image.Split(',');
                if (parts.Length == 2)
                {
                    // Extract mime type from data URI
                    var mimeMatch = parts[0];
                    if (mimeMatch.Contains("image/jpeg")) mimeType = "image/jpeg";
                    else if (mimeMatch.Contains("image/webp")) mimeType = "image/webp";
                    else if (mimeMatch.Contains("image/gif")) mimeType = "image/gif";
                    imageData = parts[1];
                }
            }

            var systemPrompt = BuildVisionSystemPrompt();

            // Build multimodal message with image
            var requestBody = new
            {
                model = visionModel,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = systemPrompt
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:{mimeType};base64,{imageData}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 500,
                temperature = 0.1
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Vision API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                    return new CardDetectionResult
                    {
                        Success = false,
                        Error = $"API hatası ({response.StatusCode})"
                    };
                }

                var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
                var reply = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";

                _logger.LogInformation("Vision AI raw response: {Reply}", reply);

                return ParseDetectedCards(reply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vision API call failed");
                return new CardDetectionResult
                {
                    Success = false,
                    Error = $"Bağlantı hatası: {ex.Message}"
                };
            }
        }

        // =============================================
        // Private Helpers
        // =============================================
        private async Task<string> CallOpenRouterAsync(object requestBody)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenRouter API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                    return $"❌ API hatası ({response.StatusCode}). Lütfen API key'inizi kontrol edin.";
                }

                var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
                return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "Yanıt alınamadı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenRouter API call failed");
                return $"❌ Bağlantı hatası: {ex.Message}";
            }
        }

        private string BuildChatSystemPrompt(object? gameState)
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

        private string BuildPokerSystemPrompt(object? gameState)
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

        private string BuildVisionSystemPrompt()
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

        private CardDetectionResult ParseDetectedCards(string aiResponse)
        {
            try
            {
                // Clean the response - remove markdown code blocks if present
                var cleaned = aiResponse.Trim();
                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned.Substring(cleaned.IndexOf('\n') + 1);
                    var lastBacktick = cleaned.LastIndexOf("```");
                    if (lastBacktick >= 0) cleaned = cleaned.Substring(0, lastBacktick);
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

                // Validate card names
                var validSuits = new HashSet<string> { "Kupa", "Karo", "Sinek", "Maça" };
                var validRanks = new HashSet<string> { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Vale", "Kız", "Papaz", "As" };

                var validCards = parsed.Cards
                    .Where(c => validSuits.Contains(c.Suit ?? "") && validRanks.Contains(c.Rank ?? ""))
                    .Select(c => new DetectedCard { Suit = c.Suit!, Rank = c.Rank! })
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
                _logger.LogWarning(ex, "Failed to parse card detection response: {Response}", aiResponse);
                return new CardDetectionResult
                {
                    Success = true,
                    Cards = new List<DetectedCard>(),
                    Message = "Kart algılama yanıtı okunamadı. Tekrar deneyin."
                };
            }
        }
    }

    // =============================================
    // DTOs
    // =============================================
    public class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    public class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
    }

    public class OpenRouterMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    // Card Detection Models
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
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blackjack.Services;

public class OpenRouterService : IAIService
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
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("HTTP-Referer"))
        {
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://blackjack-ai.local");
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("X-Title"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Blackjack AI Strategy Advisor");
        }
    }

    public async Task<string> GetChatResponseAsync(string userMessage, object? gameState = null)
    {
        var apiKey = _configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "⚠️ OpenRouter API key yapılandırılmamış. Lütfen appsettings.json dosyasına API key ekleyin.";
        }

        var model = _configuration["AI:Model"]
            ?? _configuration["OpenRouter:Model"]
            ?? "google/gemini-2.0-flash-001";

        var systemPrompt = AIMessageFactory.BuildChatSystemPrompt(gameState);
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

    public async Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null)
    {
        var apiKey = _configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "⚠️ OpenRouter API key yapılandırılmamış. Lütfen appsettings.json dosyasına API key ekleyin.";
        }

        var model = _configuration["AI:Model"]
            ?? _configuration["OpenRouter:Model"]
            ?? "google/gemini-2.0-flash-001";

        var systemPrompt = AIMessageFactory.BuildPokerSystemPrompt(gameState);
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

    public async Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image)
    {
        var apiKey = _configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new CardDetectionResult
            {
                Success = false,
                Error = "API key yapılandırılmamış."
            };
        }

        var visionModel = _configuration["AI:VisionModel"]
            ?? _configuration["OpenRouter:VisionModel"]
            ?? _configuration["OpenRouter:Model"]
            ?? "google/gemini-2.0-flash-001";

        var (mimeType, imageData) = ExtractImageData(base64Image);
        var systemPrompt = AIMessageFactory.BuildVisionSystemPrompt();

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
            return CardDetectionResponseParser.Parse(reply, _logger);
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

    private static (string MimeType, string ImageData) ExtractImageData(string base64Image)
    {
        var mimeType = "image/png";
        var imageData = base64Image;

        if (base64Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = base64Image.Split(',', 2);
            if (parts.Length == 2)
            {
                if (parts[0].Contains("image/jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    mimeType = "image/jpeg";
                }
                else if (parts[0].Contains("image/webp", StringComparison.OrdinalIgnoreCase))
                {
                    mimeType = "image/webp";
                }
                else if (parts[0].Contains("image/gif", StringComparison.OrdinalIgnoreCase))
                {
                    mimeType = "image/gif";
                }

                imageData = parts[1];
            }
        }

        return (mimeType, imageData);
    }
}

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

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blackjack.Services;

public class AnythingLlmService : IAIService
{
    private const int DefaultHistoryLimit = 100;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnythingLlmService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AnythingLlmService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AnythingLlmService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;

        var baseUrl = _configuration["AnythingLLM:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
        }

        var apiKey = _configuration["AnythingLLM:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<string> GetChatResponseAsync(string userMessage, object? gameState = null)
    {
        var validationError = ValidateConfiguration();
        if (validationError != null)
        {
            return validationError;
        }

        var mode = GetMode();
        var sessionId = BuildSessionId("blackjack");
        var historyContext = string.Equals(mode, "query", StringComparison.OrdinalIgnoreCase)
            ? await BuildHistoryContextAsync(sessionId)
            : null;

        var prompt = BuildWorkspacePrompt(
            AIMessageFactory.BuildChatSystemPrompt(gameState),
            userMessage,
            historyContext);

        var response = await SendWorkspaceChatAsync(prompt, mode, sessionId);
        return ExtractTextResponse(response);
    }

    public async Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null)
    {
        var validationError = ValidateConfiguration();
        if (validationError != null)
        {
            return validationError;
        }

        var mode = GetMode();
        var sessionId = BuildSessionId("poker");
        var historyContext = string.Equals(mode, "query", StringComparison.OrdinalIgnoreCase)
            ? await BuildHistoryContextAsync(sessionId)
            : null;

        var prompt = BuildWorkspacePrompt(
            AIMessageFactory.BuildPokerSystemPrompt(gameState),
            userMessage,
            historyContext);

        var response = await SendWorkspaceChatAsync(prompt, mode, sessionId);
        return ExtractTextResponse(response);
    }

    public async Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image)
    {
        var validationError = ValidateConfiguration();
        if (validationError != null)
        {
            return new CardDetectionResult
            {
                Success = false,
                Error = validationError
            };
        }

        var sessionId = BuildSessionId("vision");
        var attachment = BuildImageAttachment(base64Image);
        var response = await SendWorkspaceChatAsync(
            AIMessageFactory.BuildVisionSystemPrompt(),
            "chat",
            sessionId,
            new[] { attachment });

        var error = NormalizeError(response.Error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return new CardDetectionResult
            {
                Success = false,
                Error = error
            };
        }

        return CardDetectionResponseParser.Parse(response.TextResponse ?? string.Empty, _logger);
    }

    private async Task<AnythingLlmChatResponse> SendWorkspaceChatAsync(
        string message,
        string mode,
        string sessionId,
        IReadOnlyList<AnythingLlmAttachment>? attachments = null)
    {
        try
        {
            var requestBody = new Dictionary<string, object?>
            {
                ["message"] = message,
                ["mode"] = mode,
                ["sessionId"] = sessionId,
                ["reset"] = false
            };

            if (attachments is { Count: > 0 })
            {
                requestBody["attachments"] = attachments;
            }

            var workspaceSlug = Uri.EscapeDataString(GetWorkspaceSlug());
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/api/v1/workspace/{workspaceSlug}/chat", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "AnythingLLM API error: {StatusCode} - {Body}",
                    response.StatusCode,
                    responseBody);

                return new AnythingLlmChatResponse
                {
                    Error = $"API hatası ({response.StatusCode}). AnythingLLM isteği başarısız oldu."
                };
            }

            return JsonSerializer.Deserialize<AnythingLlmChatResponse>(
                       responseBody,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new AnythingLlmChatResponse
                   {
                       Error = "AnythingLLM yanıtı okunamadı."
                   };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnythingLLM API call failed");
            return new AnythingLlmChatResponse
            {
                Error = $"Bağlantı hatası: {ex.Message}"
            };
        }
    }

    private async Task<string?> BuildHistoryContextAsync(string sessionId)
    {
        var historyLimit = GetHistoryLimit();
        if (historyLimit <= 0)
        {
            return null;
        }

        try
        {
            var workspaceSlug = Uri.EscapeDataString(GetWorkspaceSlug());
            var encodedSessionId = Uri.EscapeDataString(sessionId);
            var orderBy = Uri.EscapeDataString(GetHistoryOrderBy());

            var response = await _httpClient.GetAsync(
                $"/api/v1/workspace/{workspaceSlug}/chats?apiSessionId={encodedSessionId}&limit={historyLimit}&orderBy={orderBy}");

            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AnythingLLM history fetch failed: {StatusCode} - {Body}",
                    response.StatusCode,
                    responseBody);
                return null;
            }

            var historyResponse = JsonSerializer.Deserialize<AnythingLlmHistoryResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var lines = (historyResponse?.History ?? new List<AnythingLlmHistoryItem>())
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Content)
                    && (item.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                        || item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)))
                .Select(item =>
                    $"{(item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "Asistan" : "Kullanıcı")}: {item.Content!.Trim()}")
                .ToList();

            if (lines.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            builder.AppendLine("SON KONUŞMA GEÇMİŞİ:");
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }

            return builder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AnythingLLM history fetch failed unexpectedly.");
            return null;
        }
    }

    private string? ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_configuration["AnythingLLM:BaseUrl"]))
        {
            return "⚠️ AnythingLLM BaseUrl yapılandırılmamış. Lütfen appsettings.json dosyasını kontrol edin.";
        }

        if (string.IsNullOrWhiteSpace(_configuration["AnythingLLM:ApiKey"]))
        {
            return "⚠️ AnythingLLM API key yapılandırılmamış. Lütfen appsettings.Local.json dosyasını güncelleyin.";
        }

        if (string.IsNullOrWhiteSpace(_configuration["AnythingLLM:WorkspaceSlug"]))
        {
            return "⚠️ AnythingLLM WorkspaceSlug yapılandırılmamış. Lütfen appsettings.json dosyasını kontrol edin.";
        }

        return null;
    }

    private string BuildSessionId(string conversationKey)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        string? baseSessionId = null;

        if (httpContext != null)
        {
            try
            {
                baseSessionId = httpContext.Session.Id;
            }
            catch (InvalidOperationException)
            {
                baseSessionId = httpContext.TraceIdentifier;
            }
        }

        if (string.IsNullOrWhiteSpace(baseSessionId))
        {
            baseSessionId = Guid.NewGuid().ToString("N");
        }

        return $"blackjack-{conversationKey}-{baseSessionId}";
    }

    private string GetWorkspaceSlug()
    {
        return _configuration["AnythingLLM:WorkspaceSlug"]!.Trim();
    }

    private string GetMode()
    {
        var mode = (_configuration["AnythingLLM:Mode"] ?? "query").Trim();
        return mode.Equals("chat", StringComparison.OrdinalIgnoreCase) ? "chat" : "query";
    }

    private int GetHistoryLimit()
    {
        var configuredLimit = _configuration.GetValue<int?>("AnythingLLM:HistoryLimit") ?? DefaultHistoryLimit;
        return Math.Clamp(configuredLimit, 0, 500);
    }

    private string GetHistoryOrderBy()
    {
        var orderBy = (_configuration["AnythingLLM:HistoryOrderBy"] ?? "asc").Trim();
        return orderBy.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }

    private static string BuildWorkspacePrompt(string systemPrompt, string userMessage, string? historyContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine(systemPrompt.Trim());

        if (!string.IsNullOrWhiteSpace(historyContext))
        {
            builder.AppendLine();
            builder.AppendLine(historyContext.Trim());
        }

        builder.AppendLine();
        builder.AppendLine("KULLANICI MESAJI:");
        builder.AppendLine(userMessage.Trim());

        return builder.ToString();
    }

    private static string ExtractTextResponse(AnythingLlmChatResponse response)
    {
        var error = NormalizeError(response.Error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return $"❌ {error}";
        }

        return string.IsNullOrWhiteSpace(response.TextResponse)
            ? "Yanıt alınamadı."
            : response.TextResponse;
    }

    private static string? NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error) || error.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return error;
    }

    private static AnythingLlmAttachment BuildImageAttachment(string base64Image)
    {
        var mimeType = "image/png";
        var contentString = base64Image;

        if (base64Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = base64Image.Split(',', 2);
            if (parts.Length == 2)
            {
                contentString = base64Image;

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
            }
        }
        else
        {
            contentString = $"data:{mimeType};base64,{base64Image}";
        }

        return new AnythingLlmAttachment
        {
            Name = "cards-image",
            Mime = mimeType,
            ContentString = contentString
        };
    }

    private sealed class AnythingLlmChatResponse
    {
        [JsonPropertyName("textResponse")]
        public string? TextResponse { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class AnythingLlmAttachment
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("mime")]
        public string Mime { get; set; } = "";

        [JsonPropertyName("contentString")]
        public string ContentString { get; set; } = "";
    }

    private sealed class AnythingLlmHistoryResponse
    {
        [JsonPropertyName("history")]
        public List<AnythingLlmHistoryItem>? History { get; set; }
    }

    private sealed class AnythingLlmHistoryItem
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

namespace Blackjack.Services;

public class ConfiguredAIService : IAIService
{
    private readonly OpenRouterService _openRouterService;
    private readonly AnythingLlmService _anythingLlmService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfiguredAIService> _logger;

    public ConfiguredAIService(
        OpenRouterService openRouterService,
        AnythingLlmService anythingLlmService,
        IConfiguration configuration,
        ILogger<ConfiguredAIService> logger)
    {
        _openRouterService = openRouterService;
        _anythingLlmService = anythingLlmService;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetChatResponseAsync(string userMessage, object? gameState = null)
    {
        return ResolveProvider().GetChatResponseAsync(userMessage, gameState);
    }

    public Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null)
    {
        return ResolveProvider().GetPokerChatResponseAsync(userMessage, gameState);
    }

    public Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image)
    {
        return ResolveProvider().DetectCardsFromImageAsync(base64Image);
    }

    private IAIService ResolveProvider()
    {
        var provider = (_configuration["AI:Provider"] ?? "OpenRouter").Trim();

        if (provider.Equals("AnythingLLM", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("AnythingLlm", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("anything-llm", StringComparison.OrdinalIgnoreCase))
        {
            return _anythingLlmService;
        }

        if (!provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unknown AI provider '{Provider}'. Falling back to OpenRouter.", provider);
        }

        return _openRouterService;
    }
}

namespace Blackjack.Services;

public interface IAIService
{
    Task<string> GetChatResponseAsync(string userMessage, object? gameState = null);
    Task<string> GetPokerChatResponseAsync(string userMessage, object? gameState = null);
    Task<CardDetectionResult> DetectCardsFromImageAsync(string base64Image);
}

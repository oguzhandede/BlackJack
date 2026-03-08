using System.Text.Json.Nodes;
using System.Text.Json;

namespace Blackjack.Tests;

internal static class JsonTestHelpers
{
    public static JsonNode ToJsonNode(object? value)
    {
        return JsonSerializer.SerializeToNode(value)
            ?? throw new InvalidOperationException("Test payload could not be serialized.");
    }
}

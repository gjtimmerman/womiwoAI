namespace BridgeGameCalculator.Shared.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters           = { new JsonStringEnumConverter() }
    };
}

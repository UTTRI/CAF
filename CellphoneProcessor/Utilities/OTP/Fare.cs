using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class Fare
{
    [JsonPropertyName("fare")]
    public Dictionary<string, Money>? Cost { get; set; }

    public FareComponent[]? Details { get; set; }
}

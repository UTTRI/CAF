using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class EncodedPolylineBean
{
    public string? Points { get; set; }

    public string? Levels { get; set; }

    public int Length { get; set; }
}
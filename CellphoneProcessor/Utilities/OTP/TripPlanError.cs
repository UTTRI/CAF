using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class TripPlanError
{
    public int Id { get; set; }

    public string? Msg
    {
        get; set;
    }

    public string[]? Missing { get; set; }

    public string? Message { get; set; }

    public bool NoPath { get; set; }
}

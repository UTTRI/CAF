using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class FareComponent
{
    public FeedScopeId? FareId { get; set; }

    public Money? Price { get; set; }

    public FeedScopeId[]? Routes { get; set; }
}
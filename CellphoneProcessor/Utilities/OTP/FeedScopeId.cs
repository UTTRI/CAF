using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class FeedScopeId
{
    [JsonPropertyName("agencyId")]
    public string? AgencyId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

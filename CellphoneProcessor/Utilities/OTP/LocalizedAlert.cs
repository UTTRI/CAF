using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class LocalizedAlert
{
    public string? AlertHeaderText { get; set; }

    public string? AlertDescriptionText { get; set; }

    public string? AlertUrl { get; set; }

    public int EffectiveStartDate { get; set; }
}

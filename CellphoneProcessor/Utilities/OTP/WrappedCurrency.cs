using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class WrappedCurrency
{
    public int DefaultFractionDigits { get; set; }

    public string? CurrencyCode { get; set; }

    public string? Symbol { get; set; }

    public string? Currency { get; set; }
}

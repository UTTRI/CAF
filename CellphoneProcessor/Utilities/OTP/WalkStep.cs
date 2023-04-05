using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class WalkStep
{
    public double Distance { get; set; }

    public string? RelativeDirection { get; set; }

    public string? StreetName { get; set; }

    public string? AbsoluteDirection { get; set; }

    public string? Ext { get; set; }

    public bool StayOn { get; set; }

    public bool Area { get; set; }

    public bool BogusName { get; set; }

    public double Lon { get; set; }

    public double Lat { get; set; }

    public P2OfDouble[]? Elevation { get; set; }

    public LocalizedAlert[]? Alerts { get; set; }
}
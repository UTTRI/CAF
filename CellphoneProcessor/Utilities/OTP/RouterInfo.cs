using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class RouterInfo
{
    public string? RouterId { get; set; }

    public object? Polygon{ get; set; }

    public long BuildTime{ get; set; }

    public long TransitServiceStarts{ get; set; }

    public long TransitServiceEnds{ get; set; }

    public string[]? TransitModes{ get; set; }

    public double CenterLatitude{ get; set; }

    public double CenterLongitude{ get; set; }

    public bool HasParkRide{ get; set; }

    public TravelOption[]? TravelOptions{ get; set; }

    public bool HasBikeSharing{ get; set; }

    public bool HasBikePark{ get; set; }

    public double LowerLeftLatitude{ get; set; }

    public double LowerLeftLongitude{ get; set; }

    public double UpperRightLatitude{ get; set; }

    public double UpperRightLongitude{ get; set; }

    internal static (double LowerLeftX, double LowerLeftY, double UpperRightX, double UpperRightY) GetBadBounds()
    {
        return (-1.0, -1.0, -1.0, -1.0);
    }

    internal (double LowerLeftX, double LowerLeftY, double UpperRightX, double UpperRightY) GetBounds()
    {
        if(RouterId is not null)
        {
            return (LowerLeftLongitude, LowerLeftLatitude, UpperRightLongitude, UpperRightLatitude);
        }
        return GetBadBounds();
    }
}

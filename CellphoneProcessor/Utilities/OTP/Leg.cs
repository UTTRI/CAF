using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class Leg
{
    public double StartTime { get; set; }

    public double EndTime { get; set; }

    public double DepartureDelay { get; set; }

    public double ArrivalDelay { get; set; }

    public bool RealTime { get; set; }

    public bool IsNonExactFrequency { get; set; }

    public double Headway { get; set; }

    public double Distance { get; set; }

    public bool Pathway { get; set; }

    public string? Mode { get; set; }

    public string? Route { get; set; }

    public string? AgencyName { get; set; }

    public string? AgencyUrl { get; set; }

    public string? AgencyBrandingUrl { get; set; }

    public double AgencyTimeZoneOffset { get; set; }

    public string? RouteColor { get; set; }

    public int RouteType { get; set; }

    public FeedScopeId? RouteId { get; set; }

    public string? RouteTextColor { get; set; }

    public bool InterlineWithPreviousLeg { get; set; }

    public string? TripShortName { get; set; }

    public string? TripBlockId { get; set; }

    public string? AgencyId { get; set; }

    public FeedScopeId? TripId { get; set; }

    public string? ServiceDate { get; set; }

    public string? RouteBrandingUrl { get; set; }

    public Place? From { get; set; }

    public Place? To { get; set; }

    public Place[]? IntermediateStops { get; set; }

    public EncodedPolylineBean? LegGeometry { get; set; }

    public WalkStep[]? Steps { get; set; }

    public LocalizedAlert[]? Alerts { get; set; }

    public string? RouteShortName { get; set; }

    public string? RouteLongName { get; set; }

    public string? BoardRule { get; set; }

    public string? AlightRule { get; set; }

    public bool RentedBike { get; set; }

    public bool CallAndRide { get; set; }

    public double FlexCallAndRideMaxStartTime { get; set; }

    public double FlexCallAndRideMaxEndTime { get; set; }

    public double FlexDrtAdvanceBookMin { get; set; }

    public string? FlexDrtPickupMessage { get; set; }

    public string? FlexDrtDropOffMessage { get; set; }

    public bool TransitLeg { get; set; }

    public double Duration { get; set; }
}

using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class Place
{
    public string? Name { get; set; }

    // TODO: Figure out why it crashes when we try to read in the stop id
    // public FeedScopeId? StopId { get; set; }
    //[JsonPropertyName("stopId")]
    //public Dictionary<string, string>? StopId { get; set; }

    public string? StopCode { get; set; }

    public string? PlatformCode { get; set; }

    public double Lon { get; set; }

    public double Lat { get; set; }

    public double Arrival { get; set; }

    public double Departure { get; set; }

    public string? Orig { get; set; }

    public string? ZoneId { get; set; }

    public int StopIndex { get; set; }

    public int StopSequence { get; set; }

    public string? VertexType { get; set; }

    public string? BikeShareId { get; set; }

    public string? BoardAlightType { get; set; }

    public EncodedPolylineBean? FlagStopArea { get; set; }
}
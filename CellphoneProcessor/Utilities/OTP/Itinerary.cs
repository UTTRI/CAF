using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class Itinerary
{
    public double Duration { get; set; }

    public double StarTime { get; set; }

    public double EndTime { get; set; }

    public double WalkTime { get; set; }

    public double TransitTime { get; set; }

    public double WaitingTime { get; set; }

    public double WalkDistance { get; set; }

    public bool WalkLimitExceeded { get; set; }

    public double ElevationLost { get; set; }

    public double ElevationGained { get; set; }

    public int Transfers { get; set; }

    /* TODO: Implement the rest of the features
     * public Fare? Fare { get; set; }

    public Leg[]? Legs { get; set; }
    */

    public bool TooSloped { get; set; }
}
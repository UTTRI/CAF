using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class TripPlan
{
    public long Date { get; set; }

    public Place? From { get; set; }

    public Place? To { get; set; }

    public Itinerary[]? Itineraries { get; set; }

    /// <summary>
    /// Gets the TransitLoS out of the TripPlan.
    /// </summary>
    /// <returns>The LoS for the first itinerary, or an invalid LoS if none exist.</returns>
    public TransitLoS GetLoS()
    {
        if (Itineraries is not null && Itineraries.Length > 0)
        {
            // Make sure to convert from seconds to minutes
            return new TransitLoS(Itineraries[0].Transfers,
                Itineraries[0].TransitTime / 60.0,
                Itineraries[0].WalkTime / 60.0,
                Itineraries[0].WaitingTime / 60.0);
        }
        return TransitLoS.GetBadRequest();
    }
}

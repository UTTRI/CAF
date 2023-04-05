using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class GetPlanResponse
{
    public TripPlan? Plan { get; set; }

    public Dictionary<string, string>? RequestParameters { get; set; }

    public Dictionary<string, object>? DebugOutput { get; set; }

    public TripPlanError? Error
    {
        get;
        set;
    }

    public TransitLoS GetLoS()
    {
        if(Plan is TripPlan plan)
        {
             return plan.GetLoS();
        }
        else if(Error?.Message == "TOO_CLOSE")
        {
            return TransitLoS.GetIntrazonal();
        }
        else
        {
            return TransitLoS.GetBadRequest();
        }
    }
}


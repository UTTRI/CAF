using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class RoutersResponse
{
    public RouterInfo[]? RouterInfo { get; set; }

    internal List<string> GetNames()
    {
        if(RouterInfo == null)
        {
            return new List<string>();
        }
        // We know that the nulls and white-spaces have been removed
        return RouterInfo.Select(routerInfo => routerInfo.RouterId)
                         .Where(name => !String.IsNullOrWhiteSpace(name))
                         .ToList()!; 
    }
}

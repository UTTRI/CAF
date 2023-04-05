using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CellphoneProcessor.Utilities.OTP;


namespace CellphoneProcessor.Utilities;

/// <summary>
/// This class is designed to facilitate the calls to OTP
/// to get transit LoS.
/// </summary>
public sealed class OpenTripPlanner : IDisposable
{
    private readonly HttpClient _client = new();
    private string _routerPath = string.Empty;

    public OpenTripPlanner(string uri)
    {
        _client.BaseAddress = new Uri(uri);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Set the router for use when getting transit plans back.
    /// Call GetRoutersAsync to get the list of available routers.
    /// </summary>
    /// <param name="routerId">The name of the router to use.</param>
    public void SetRouter(string routerId)
    {
        _routerPath = $"/otp/routers/{routerId}/plan";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="originX"></param>
    /// <param name="originY"></param>
    /// <param name="destinationX"></param>
    /// <param name="destinationY"></param>
    /// <param name="startTime"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">This exception is thrown if you have not already set the routerId to use.</exception>
    public async Task<TransitLoS> GetPlanAsync(double originX, double originY, double destinationX, double destinationY, string startTime, string date)
    {
        if (String.IsNullOrWhiteSpace(_routerPath))
        {
            throw new InvalidOperationException("The router must be set before calling " + nameof(GetPlanAsync));
        }
        // numItineraries=1&
        // &&minTransferTime=600
        // &&walkReluctance=5
        // &&showIntermediateStops=False
        // http://emme-windows.vaughan.local:8080/otp/routers/current/plan?fromPlace=4.55213,-74.09454&toPlace=4.50483,-74.10553&time=1:02pm&date=04-02-2019&mode=TRANSIT,WALK&maxWalkDistance=500&arriveBy=false
        // HttpResponseMessage response = await _client.GetAsync($"{_routerPath}?numItineraries=1&&mode=TRANSIT,WALK&&fromPlace={originY},{originX}" +
        //     $"&&toPlace={destinationY},{destinationX}&&maxWalkDistance=1600" +
        //     $"&&date=04-02-2019&&time=1:02pm&&arriveBy=false&&showIntermediateStops=false&&walkReluctance=5&&waitReluctance=1&&minTransferTime=600");
        HttpResponseMessage response = await _client.GetAsync($"{_routerPath}?fromPlace={originX},{originY}&toPlace={destinationX},{destinationY}&time={startTime}&date={date}&mode=TRANSIT,WALK&maxWalkDistance=1600&arriveBy=false" +
            $"&numItineraries=1&&showIntermediateStops=false&walkReluctance=5&minTransferTime=600");
        return response.IsSuccessStatusCode
            ? ((await response.Content.ReadFromJsonAsync<GetPlanResponse>())?.GetLoS() ?? TransitLoS.GetBadRequest())
            : TransitLoS.GetBadRequest();
    }

    /// <summary>
    /// Gets the list of all of the different routers available on the server.
    /// </summary>
    /// <returns>A list of routers that are currently available on the server.</returns>
    public async Task<List<string>> GetRoutersAsync()
    {
        HttpResponseMessage response = await _client.GetAsync("otp/routers");
        return response.IsSuccessStatusCode ?
              ((await response.Content.ReadFromJsonAsync<RoutersResponse>())?.GetNames() ?? new List<string>())
            : new List<string>();
    }

    public async Task<(double LowerLeftX, double LowerLeftY, double UpperRightX, double UpperRightY)> GetRouterBounds(string routerId)
    {
        HttpResponseMessage response = await _client.GetAsync($"otp/routers/{routerId}");
        return response.IsSuccessStatusCode ?
              ((await response.Content.ReadFromJsonAsync<RouterInfo>())?.GetBounds() ?? RouterInfo.GetBadBounds())
            : RouterInfo.GetBadBounds();
    }

    private bool _disposedValue;

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _client.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

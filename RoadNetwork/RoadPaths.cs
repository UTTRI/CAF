namespace RoadNetwork;

/// <summary>
/// Provides an aggregation for storing the paths for each OD
/// going through the Road Network.
/// </summary>
public sealed class RoadPaths
{
    private readonly List<int>[] _paths;
    private float[] _costs;
    private float[] _previousCosts;

    private readonly int _numberOfZones;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="zoneSystem"></param>
    public RoadPaths(ZoneSystem zoneSystem)
    {
        _numberOfZones = zoneSystem.Length;
        _paths = new List<int>[_numberOfZones * _numberOfZones];
        _costs = new float[_numberOfZones * _numberOfZones];
        _previousCosts = new float[_numberOfZones * _numberOfZones];
        for (int i = 0; i < _paths.Length; i++)
        {
            _paths[i] = new List<int>();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    public List<int> GetPath(int origin, int destination)
    {
        return _paths[origin * _numberOfZones + destination];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    public ref float GetCost(int origin, int destination)
    {
        return ref _costs[origin * _numberOfZones + destination];
    }

    /// <summary>
    /// Setup for running the next iteration
    /// </summary>
    public void UpdateForNextIteration()
    {
        for (int i = 0; i < _paths.Length; i++)
        {
            _paths[i].Clear();
        }
        // Swap our previous times
        var temp = _costs;
        _costs = _previousCosts;
        _previousCosts = temp;
    }

    /// <summary>
    /// This function takes the sum of the first derivative of the
    /// cost differences.  Only call this function after having
    /// updated the iteration at least once.
    /// </summary>
    /// <param name="demand">The demand matrix.</param>
    /// <param name="stepSize">The size of the step to test.</param>
    /// <returns>The sum of the first derivative for the costs</returns>
    public float SumFirstDerivative(Matrix demand, float stepSize)
    {
        var d = demand.Data;
        var current = _costs;
        var previous = _previousCosts;
        var acc = 0.0;
        for (int i = 0; i < d.Length; i++)
        {
            acc += d[i] 
                * (stepSize * current[i] + (1 - stepSize) * previous[i]) 
                * (current[i] - previous[i]);
        }
        return (float)acc;
    }

    /// <summary>
    /// Compute the relative gap for this iteration.
    /// </summary>
    /// <param name="network">The updated network</param>
    /// <returns>The relative gap.</returns>
    internal float ComputeRelativeGap(Network network, Matrix demand)
    {
        // The relative gap is the ratio of the travel times on the current network
        var d = demand.Data;
        var updatedTT = 0.0f;
        var originalTT = 0.0f;
        for(int i = 0; i < _paths.Length; i++)
        {
            updatedTT += d[i] * network.GetTravelTime(_paths[i]);
            originalTT += d[i] * _costs[i];
        }
        return (updatedTT / originalTT) - 1;
    }
}

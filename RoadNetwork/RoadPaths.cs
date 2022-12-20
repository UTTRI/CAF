namespace RoadNetwork;

/// <summary>
/// Provides an aggregation for storing the paths for each OD
/// going through the Road Network.
/// </summary>
public sealed class RoadPaths
{
    private readonly List<int>[] _paths;
    private readonly float[] _costs;

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
    /// 
    /// </summary>
    public void ClearPaths()
    {
        for (int i = 0; i < _paths.Length; i++)
        {
            _paths[i].Clear();
        }
    }
}

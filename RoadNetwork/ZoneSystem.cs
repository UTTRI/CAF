namespace RoadNetwork;

/// <summary>
/// Use this to map nodes as TAZ for use in a Road Assignment.
/// </summary>
public sealed class ZoneSystem
{
    /// <summary>
    /// A lookup from sparse zone number to the flat zone index.
    /// </summary>
    private readonly Dictionary<int, int> _zoneNumberLookup = new();

    /// <summary>
    /// A lookup between flat zone indexes to the node that will represent the zone.
    /// </summary>
    private readonly int[] _nodeNumber;

    /// <summary>
    /// Construct a zone system from the given file name
    /// </summary>
    /// <param name="fileName">
    /// The path of the file to load the zone system from.
    /// </param>
    public ZoneSystem(string fileName)
    {
        var zones = File.ReadLines(fileName)
            .Skip(1)
            .Select(x => x.Split(','))
            .Where(x => x.Length >= 2)
            .Select(x => (ZoneNumber: int.Parse(x[0]), Centroid: int.Parse(x[1])))
            .OrderBy(x => x.ZoneNumber)
            .ToArray();

        Length = zones.Length;
        _nodeNumber = new int[Length];
        for (int i = 0; i < zones.Length; i++)
        {
            _zoneNumberLookup[zones[i].ZoneNumber] = i;
            _nodeNumber[i] = zones[i].Centroid;
        }
    }

    /// <summary>
    /// The number of zones in the zone system
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the node number to use for the given zone index
    /// </summary>
    /// <param name="zoneIndex">The flat index for the zone to lookup.</param>
    /// <returns>The node number to use for the zone.</returns>
    public int GetNodeForZoneIndex(int zoneIndex) => _nodeNumber[zoneIndex];

    /// <summary>
    /// Gets the flat index number of the given sparse zone number.
    /// </summary>
    /// <param name="zoneNumber">The sparse zone number to get the index for.</param>
    /// <returns>The flat index number of </returns>
    internal int GetIndexForZoneNumber(int zoneNumber)
        => _zoneNumberLookup.TryGetValue(zoneNumber, out var zone) ? zone : -1;
}

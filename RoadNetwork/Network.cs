using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RTree;


namespace RoadNetwork;

public sealed class Network
{
    private readonly List<Node> _nodes;
    private readonly Link[] _links;
    private readonly float[] _lanes;
    private readonly float[] _capacity;
    private readonly float[] _exponent;
    private readonly int[] _nodeOffset;
    private readonly int[] _linkCounts;
    private readonly RTree<int> _nodeLookup;
    private const int CacheChunkSize = 1024;
    public Network(string fileName)
    {
        var requestedFile = new FileInfo(fileName);
        if (HasCachedVersion(requestedFile))
        {
            Console.WriteLine("Cached network found.");
            _nodes = LoadCachedVersion(requestedFile);
        }
        else
        {
            Console.WriteLine("No Cached network found, loading raw network.");
            if (!requestedFile.Exists)
            {
                throw new FileNotFoundException(fileName);
            }
            _nodes = OSMLoader.LoadOSMNetwork(fileName);
            Console.WriteLine("Network Loaded, storing cached version.");
            SaveCachedVersion(requestedFile);
        }
        Console.WriteLine("Building indexes");
        (_nodeLookup, _nodeOffset, _linkCounts, _links)
            = BuildIndexes(_nodes);
        // TOOD: Actually load in the links properly
        _lanes = new float[_links.Length];
        Array.Fill(_lanes, 1.0f);
        (_capacity, _exponent)  = ComputeCapacity();
    }

    private (float[] Capacity, float[] Exponent) ComputeCapacity()
    {
        var capacity = new float[_links.Length];
        var exponent = new float[_links.Length];
        for (int i = 0; i < capacity.Length; i++)
        {
            switch (_links[i].RoadType)
            {
                case HighwayType.Motorway:
                case HighwayType.MotorwayLink:
                    capacity[i] = 2000.0f;
                    exponent[i] = 6.0f;
                    break;
                case HighwayType.Trunk:
                case HighwayType.TrunkLink:
                    capacity[i] = 2000.0f;
                    exponent[i] = 6.0f;
                    break;
                case HighwayType.Primary:
                case HighwayType.PrimaryLink:
                    capacity[i] = 1400.0f;
                    exponent[i] = 4.0f;
                    break;
                case HighwayType.Secondary:
                case HighwayType.SecondaryLink:
                    capacity[i] = 800.0f;
                    exponent[i] = 4.0f;
                    break;
                default:
                    capacity[i] = 500.0f;
                    exponent[i] = 4.0f;
                    break;
            }
        }
        return (capacity, exponent);
    }

    private static (RTree<int> _nodeLookup, int[] _nodeOffset, int[] _linkCounts, Link[] _links) BuildIndexes(List<Node> nodes)
    {
        RTree<int>? _nodeLookup = null;
        int[]? _nodeOffset = null;
        int[]? _linkCounts = null;
        Link[]? _links = null;
        Parallel.Invoke(() =>
        {

            _nodeLookup = new RTree<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                _nodeLookup.Add(new Rectangle(node.Lat, node.Lon, node.Lat, node.Lon, 0, 0), i);
            }

        }, () =>
        {
            (_nodeOffset, _linkCounts, _links) = CreateLinkTable(nodes);
        });
        return (_nodeLookup!, _nodeOffset!, _linkCounts!, _links!);
    }

    public int NodeCount => _nodes.Count;

    public int LinkCount => _links.Length;

    private static bool HasCachedVersion(FileInfo requestedFile)
    {
        return File.Exists(GetCachedName(requestedFile));
    }

    private static string GetCachedName(FileInfo requestedFile) => requestedFile.FullName + ".cached";

    private static List<Node> LoadCachedVersion(FileInfo requestedFile)
    {
        var watch = Stopwatch.StartNew();
        using var reader = new BinaryReader(File.OpenRead(GetCachedName(requestedFile)));
        var magicNumber = reader.ReadInt64();
        if (magicNumber != 6473891448)
        {
            throw new Exception("Invalid Magic Number!");
        }
        var numberOfNodes = reader.ReadInt32();
        var numberOfLinks = new int[numberOfNodes];
        var ret = new List<Node>(numberOfNodes);
        Console.WriteLine($"Number of nodes {numberOfNodes}");
        for (int i = 0; i < numberOfNodes; i++)
        {
            var lat = reader.ReadSingle();
            var lon = reader.ReadSingle();
            numberOfLinks[i] = reader.ReadInt32();
            ret.Add(new Node(lat, lon, new List<Link>(numberOfLinks[i])));
        }
        var linkSum = 0;
        for (int i = 0; i < numberOfNodes; i++)
        {
            for (int j = 0; j < numberOfLinks[i]; j++)
            {
                linkSum++;
                var destination = reader.ReadInt32();
                var time = reader.ReadSingle();
                var roadType = (HighwayType)reader.ReadInt32();
                ret[i].Connections.Add(new Link(destination, time, roadType));
            }
        }
        watch.Stop();
        Console.WriteLine($"Number of links {linkSum}");
        Console.WriteLine($"Network load time = {watch.ElapsedMilliseconds}ms");
        return ret;
    }

    private static (int[] nodeOffset, int[] linkCount, Link[]) CreateLinkTable(List<Node> nodes)
    {
        var linkLookup = new RTree<int>();
        var nodeOffset = new int[nodes.Count];
        var linkCount = new int[nodes.Count];
        var numberOfLinks = nodes.Sum(n => n.Connections.Count);
        var links = new Link[numberOfLinks];
        int currentPosition = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            nodeOffset[i] = currentPosition;
            linkCount[i] = nodes[i].Connections.Count;
            for (int j = 0; j < nodes[i].Connections.Count; j++)
            {
                links[currentPosition] = nodes[i].Connections[j];
                currentPosition++;
            }
        }
        return (nodeOffset, linkCount, links);
    }

    private void SaveCachedVersion(FileInfo requestedFile)
    {
        using var writer = new BinaryWriter(File.OpenWrite(GetCachedName(requestedFile)));
        // magic number
        writer.Write(6473891448L);
        writer.Write(_nodes.Count);
        Console.WriteLine($"Number of nodes {_nodes.Count}");
        for (int i = 0; i < _nodes.Count; i++)
        {
            writer.Write(_nodes[i].Lat);
            writer.Write(_nodes[i].Lon);
            writer.Write(_nodes[i].Connections.Count);
        }
        int numberOfLinks = 0;
        for (int i = 0; i < _nodes.Count; i++)
        {
            for (int j = 0; j < _nodes[i].Connections.Count; j++)
            {
                writer.Write(_nodes[i].Connections[j].Destination);
                writer.Write(_nodes[i].Connections[j].Time);
                writer.Write((int)_nodes[i].Connections[j].RoadType);
            }
            numberOfLinks += _nodes[i].Connections.Count;
        }
        Console.WriteLine($"Number of links {numberOfLinks}");
    }

    /// <summary>
    /// Approximates the distance between two lat/lon points.  
    /// Relatively accurate within ~4000KM.
    /// https://en.wikipedia.org/wiki/Haversine_formula
    /// https://rosettacode.org/wiki/Haversine_formula#C++
    /// </summary>
    /// <param name="lat1"></param>
    /// <param name="lon1"></param>
    /// <param name="lat2"></param>
    /// <param name="lon2"></param>
    /// <returns>The distances are in KMs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static float ComputeDistance(float lat1, float lon1, float lat2, float lon2)
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        static double DegreeToRadian(float deg) => deg * (Math.PI / 180.0f);

        const double earthRadius = 6371.0; // Radius of the earth in km
        double latRad1 = DegreeToRadian(lat1);
        double latRad2 = DegreeToRadian(lat2);
        double lonRad1 = DegreeToRadian(lon1);
        double lonRad2 = DegreeToRadian(lon2);

        double diffLa = latRad2 - latRad1;
        double doffLo = lonRad2 - lonRad1;

        double computation = Math.Asin(Math.Sqrt(Math.Sin(diffLa / 2) * Math.Sin(diffLa / 2)
            + Math.Cos(latRad1) * Math.Cos(latRad2) * Math.Sin(doffLo / 2) * Math.Sin(doffLo / 2)));
        return (float)(2 * earthRadius * computation);
    }

    public (float time, float distance, HighwayType originRoad, HighwayType destinationRoad) Compute(float originX, float originY, float destinationX, float destinationY, int[] cache, bool[] dirtyBits)
    {
        // Find closest origin node in the network
        int originNodeIndex = FindClosestNodeIndex(originX, originY);
        // Find closest destination node in the network
        int destinationNodeIndex = FindClosestNodeIndex(destinationX, destinationY);
        if (originNodeIndex == destinationNodeIndex)
        {
            // we don't need to clear the cache if we don't use the fastest path algorithm.
            return (0, 0, HighwayType.NotRoad, HighwayType.NotRoad);
        }

        // Find the fastest route between the two points
        var path = GetFastestPathDijkstra(originNodeIndex, destinationNodeIndex, cache, dirtyBits);
        if (path is null)
        {
            return (-1, -1, HighwayType.NotRoad, HighwayType.NotRoad);
        }

        // Compute the travel time and distance for the fastest path
        var distance = 0.0f;
        var time = 0.0f;
        var previousDestinationIndex = originNodeIndex;
        for (int i = 0; i < path.Count; i++)
        {
            var origin = _nodes[path[i].origin];
            int destinationIndex = path[i].destination;
            var destination = _nodes[destinationIndex];
            distance += ComputeDistance(origin.Lat, origin.Lon, destination.Lat, destination.Lon);
            for (int j = 0; j < origin.Connections.Count; j++)
            {
                if (origin.Connections[j].Destination == destinationIndex)
                {
                    time += origin.Connections[j].Time;
                    break;
                }
            }
        }
        var (originRoadType, destinationRoadType) = path.Count switch
        {
            0 => (HighwayType.NotRoad, HighwayType.NotRoad),
            1 => GetRoadTypes(originX, originY, originNodeIndex),
            2 => (_nodes[path[0].origin].Connections.FirstOrDefault(c => c.Destination == destinationNodeIndex).RoadType,
                    _nodes[path[0].origin].Connections.FirstOrDefault(c => c.Destination == destinationNodeIndex).RoadType),
            _ => (_nodes[path[0].origin].Connections.FirstOrDefault(c => c.Destination == path[0].destination).RoadType,
                   _nodes[path[^1].origin].Connections.FirstOrDefault(c => c.Destination == path[^1].destination).RoadType)
        };
        // figure out the distance to and from the road network
        var distanceToAndFrom = ComputeDistance(originX, originY, _nodes[originNodeIndex].Lat, _nodes[originNodeIndex].Lon)
            + ComputeDistance(_nodes[destinationNodeIndex].Lat, _nodes[destinationNodeIndex].Lon, destinationX, destinationY);
        // Add the time to and from the road network assuming 40km/h
        return (time + (distanceToAndFrom * (40.0f / 60.0f)), distance + distanceToAndFrom, originRoadType, destinationRoadType);
    }

    /// <summary>
    /// Get the travel time for a given path
    /// </summary>
    /// <param name="path">The path in order to get the travel times from.</param>
    /// <returns>The total travel time for the path.</returns>
    public float GetTravelTime(List<(int origin, int destination)>? path)
    {
        if (path is null) return -1;
        if (path.Count == 0) return 0f;

        var time = 0.0f;
        for (int i = 0; i < path.Count; i++)
        {
            var origin = _nodes[path[i].origin];
            int destinationIndex = path[i].destination;
            for (int j = 0; j < origin.Connections.Count; j++)
            {
                if (origin.Connections[j].Destination == destinationIndex)
                {
                    time += origin.Connections[j].Time;
                    break;
                }
            }
        }
        return time;
    }

    /// <summary>
    /// Get the travel time for a given path
    /// </summary>
    /// <param name="path">The path in order to get the travel times from.</param>
    /// <returns>The total travel time for the path.</returns>
    public float GetTravelTime(List<int>? path)
    {
        if (path is null) return -1;
        if (path.Count == 0) return 0f;

        var time = 0.0f;
        var origin = _nodes[path[0]];
        for (int i = 1; i < path.Count; i++)
        {
            int destinationIndex = path[i];
            for (int j = 0; j < origin.Connections.Count; j++)
            {
                if (origin.Connections[j].Destination == destinationIndex)
                {
                    time += origin.Connections[j].Time;
                    break;
                }
            }
            origin = _nodes[destinationIndex];
        }
        return time;
    }

    private (HighwayType originRoadType, HighwayType destinationRoadType) GetRoadTypes(float x, float y, int nodeIndex)
    {
        HighwayType ret = HighwayType.NotRoad;
        float closest = float.PositiveInfinity;
        foreach (var link in _nodes[nodeIndex].Connections)
        {
            var distance = ComputeDistance(x, y, _nodes[link.Destination].Lat, _nodes[link.Destination].Lon);
            if (distance < closest)
            {
                closest = distance;
                ret = link.RoadType;
            }
        }
        return (ret, ret);
    }

    private static void ClearCache(int[] cache, bool[] dirtyBits)
    {
        // only clean the dirty sections
        for (int i = 0; i < dirtyBits.Length; i++)
        {
            if (dirtyBits[i])
            {
                Array.Fill(cache, -1, i * CacheChunkSize, Math.Min(CacheChunkSize, cache.Length - i * CacheChunkSize));
                dirtyBits[i] = false;
            }
        }
    }

    /// <summary>
    /// Finds the closest node to the given coordinates.
    /// </summary>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <returns>The index of the node that is the closest.</returns>
    private int FindClosestNodeIndex(float lat, float lon)
    {
        var closest = _nodeLookup.Nearest(new Point(lat, lon, 0), 100);
        if (closest is not null && closest.Count > 0)
        {
            return closest[0];
        }
        else
        {
            // The backup strategy for finding the closest node
            int min = -1;
            float minDistancce = float.PositiveInfinity;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var distance = ComputeDistance(lat, lon, _nodes[i].Lat, _nodes[i].Lon);
                if (distance < minDistancce)
                {
                    min = i;
                    minDistancce = distance;
                }
            }
            if (min == -1)
            {
                Console.WriteLine("No node found!");
            }
            return min;
        }
    }

    /// <summary>
    /// Thread-safe on a static network
    /// </summary>
    /// <param name="originNodeIndex"></param>
    /// <param name="destinationNodeIndex"></param>
    /// <returns></returns>
    public unsafe List<(int origin, int destination)>? GetFastestPathDijkstra(int originNodeIndex, int destinationNodeIndex, int[] fastestParent, bool[] dirtyBits)
    {
        if (originNodeIndex == destinationNodeIndex)
        {
            return new();
        }
        ClearCache(fastestParent, dirtyBits);
        MinHeap toExplore = new();
        fixed (int* fp = fastestParent)
        fixed (bool* db = dirtyBits)
        fixed (int* no = _nodeOffset)
        fixed (int* lc = _linkCounts)
        fixed (Link* l = _links)
        {
            fp[originNodeIndex] = originNodeIndex;
            db[originNodeIndex / CacheChunkSize] = true;
            foreach (var link in _nodes[originNodeIndex].Connections)
            {
                toExplore.Push(link.Destination, originNodeIndex, link.Time);
            }
            while (toExplore.Count > 0)
            {
                var (Destination, Origin, Cost) = toExplore.PopMin();
                // don't explore things that we have already done
                if (fp[Destination] != -1)
                {
                    continue;
                }
                fp[Destination] = Origin;
                db[Destination / CacheChunkSize] = true;
                // check to see if we have hit our destination
                if (Destination == destinationNodeIndex)
                {
                    return GeneratePath(fastestParent, destinationNodeIndex);
                }
                
                var nodeOffset = no[Destination];
                for (int i = 0; i < lc[Destination]; i++)
                {
                    // explore everything that hasn't been solved, the min heap will update if it is a faster path to the child node
                    if (fp[l[nodeOffset + i].Destination] == -1)
                    {
                        // make sure cars are allowed on the link
                        var linkCost = l[nodeOffset + i].Time;
                        if (linkCost >= 0)
                        {
                            toExplore.Push(l[nodeOffset + i].Destination, Destination, Cost + linkCost);
                        }
                    }
                }
            }
        }
        return null;
    }

    private unsafe void FillDistances(float* distances, int destinationNodeIndex)
    {
        var destLat = _nodes[destinationNodeIndex].Lat;
        var destLon = _nodes[destinationNodeIndex].Lon;
        for (int i = 0; i < _nodes.Count; i++)
        {
            distances[i] = ComputeDistance(_nodes[i].Lat, _nodes[i].Lon, destLat, destLon);
        }
    }

    private static List<(int origin, int destination)> GeneratePath(int[] fastestParent,
            int destination)
    {
        // unwind the parents to build the path
        var ret = new List<(int, int)>();
        var prev = destination;
        while (prev > 0)
        {
            var next = fastestParent[prev];
            // break if we hit a cycle often found at the origin.
            if (next == prev)
            {
                break;
            }
            ret.Add((next, prev));
            prev = next;
        }
        // reverse the list before returning it
        ret.Reverse();
        return ret;
    }

    public (int[] fastestPath, bool[] dirtyBits) GetCache()
    {
        var fp = new int[_nodes.Count];
        var dirty = new bool[(int)Math.Ceiling((float)_nodes.Count / CacheChunkSize)];
        Array.Fill(fp, -1);
        Array.Fill(dirty, false);
        return (fp, dirty);
    }

    /// <summary>
    /// Modify the link travel times to the given values
    /// </summary>
    /// <param name="volumes">The new travel times, the length of this array must be the same size as the number of links.</param>
    public void UpdateLinkTravelTimes(float[] volumes, float[] freeFlowTime)
    {
        if (volumes.Length != _links.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(volumes), "The size of the times must be the same as the number of links in the network.");
        }
        for (int i = 0; i < volumes.Length; i++)
        {
            // (length*60/ul2)*((1+put((volau+volad+el1)/(lanes*ul3))^6)*(get(1).le.1)+(6*get(1)-4)*(get(1).gt.1))
            var ratio = volumes[i] / (_capacity[i] * _lanes[i]);
            _links[i].Time =
                freeFlowTime[i] * (ratio <= 1 ? (1.0f + MathF.Pow(ratio * _lanes[i], _exponent[i])) : (_exponent[i] * ratio - (_exponent[i] - 2.0f)));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public float[] GetTimes()
    {
        return _links.Select(link => link.Time).ToArray();
    }

    public void SaveNetwork(string outputNetworkFile)
    {
        //throw new NotImplementedException();
    }
}

/// <summary>
/// Represents a point in space
/// </summary>
/// <param name="Lat">Latitude</param>
/// <param name="Lon">Longitude</param>
/// <param name="Connections">A list of connections between nodes.</param>
internal record struct Node(float Lat, float Lon, List<Link> Connections);

/// <summary>
/// Represents a connection between nodes and the associated travel time
/// between those nodes.
/// </summary>
/// <param name="Destination"></param>
/// <param name="Time"></param>
/// <param name="RoadType"></param>
internal record struct Link(int Destination, float Time, HighwayType RoadType);

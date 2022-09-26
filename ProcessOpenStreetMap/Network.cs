using System.Buffers;
using System.Runtime.CompilerServices;
using RTree;


namespace ProcessOpenStreetMap;

internal sealed class Network
{
    private readonly List<Node> _nodes;
    private readonly Link[] _links;
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

    private static bool HasCachedVersion(FileInfo requestedFile)
    {
        return File.Exists(GetCachedName(requestedFile));
    }

    private static string GetCachedName(FileInfo requestedFile) => requestedFile.FullName + ".cached";

    private static List<Node> LoadCachedVersion(FileInfo requestedFile)
    {
        using var reader = new BinaryReader(File.OpenRead(GetCachedName(requestedFile)));
        var magicNumber = reader.ReadInt64();
        if (magicNumber != 6473891447)
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
                ret[i].Connections.Add(new Link(destination, time));
            }
        }
        Console.WriteLine($"Number of links {linkSum}");
        return ret;
    }

    private static (int[] nodeOffset, int[] linkCount, Link[]) CreateLinkTable(List<Node> nodes)
    {
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
        writer.Write(6473891447L);
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
                numberOfLinks++;
                writer.Write(_nodes[i].Connections[j].Destination);
                writer.Write(_nodes[i].Connections[j].Time);
            }
        }
        Console.WriteLine($"Number of links {numberOfLinks}");
    }

    /// <summary>
    /// Approximates the distance between two lat/lon points.  
    /// Relatively accurate within ~4000KM.
    /// https://en.wikipedia.org/wiki/Haversine_formula
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
        static float ToRads(float deg) => deg * (MathF.PI / 180.0f);

        const float earthRadius = 6371.0f; // Radius of the earth in km
        var dLat = ToRads(lat2 - lat1);  // deg2rad below
        var dLon = ToRads(lon2 - lon1);
        var a =
          MathF.Sin(dLat / 2.0f) * MathF.Sin(dLat / 2.0f) +
          MathF.Cos(ToRads(lat1)) * MathF.Cos(ToRads(lat2)) *
          MathF.Sin(dLon / 2.0f) * MathF.Sin(dLon / 2.0f)
          ;
        var c = 2 * MathF.Atan2(MathF.Sqrt(a), MathF.Sqrt(1.0f - a));
        var d = earthRadius * c; // Distance in km
        return d;
    }

    public (float time, float distance) Compute(float originX, float originY, float destinationX, float destinationY, int[] cache, bool[] dirtyBits)
    {
        // Find closest origin node in the network
        int originNodeIndex = FindClosestNodeIndex(originX, originY);
        // Find closest destination node in the network
        int destinationNodeIndex = FindClosestNodeIndex(destinationX, destinationY);
        if (originNodeIndex == destinationNodeIndex)
        {
            // we don't need to clear the cache if we don't use the fastest path algorithm.
            return (0, 0);
        }
        // Find the fastest route between the two points
        var path = GetFastestPathDijkstra(originNodeIndex, destinationNodeIndex, cache, dirtyBits);
        if (path is null)
        {
            return (-1, -1);
        }
        // Compute the travel time and distance for the fastest path
        var distance = 0.0f;
        var time = 0.0f;
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
        return (time, distance);
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
                var current = toExplore.PopMin();
                // don't explore things that we have already done
                int currentDestination = current.Destination;
                // if there was already a faster way to get here continue
                if (fp[currentDestination] != -1)
                {
                    continue;
                }
                fp[currentDestination] = current.Origin;
                db[currentDestination / CacheChunkSize] = true;
                // check to see if we have hit our destination
                if (currentDestination == destinationNodeIndex)
                {
                    return GeneratePath(fastestParent, destinationNodeIndex);
                }
                // foreach (var childDestination in links)
                var nodeOffset = no[currentDestination];
                for (int i = 0; i < lc[currentDestination]; i++)
                {
                    // explore everything that hasn't been solved, the min heap will update if it is a faster path to the child node
                    if (fp[l[nodeOffset + i].Destination] == -1)
                    {
                        // make sure cars are allowed on the link
                        var linkCost = l[nodeOffset + i].Time;
                        if (linkCost >= 0)
                        {
                            toExplore.Push(l[nodeOffset + i].Destination, currentDestination, current.Cost + linkCost);
                        }
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Thread-safe on a static network
    /// </summary>
    /// <param name="originNodeIndex"></param>
    /// <param name="destinationNodeIndex"></param>
    /// <returns></returns>
    public unsafe List<(int origin, int destination)>? GetFastestPathAStar(int originNodeIndex, int destinationNodeIndex, int[] fastestParent, bool[] dirtyBits)
    {
        if (originNodeIndex == destinationNodeIndex)
        {
            return new();
        }
        ClearCache(fastestParent, dirtyBits);
        MinHeapAStar toExplore = new();
        var finalDestLat = _nodes[destinationNodeIndex].Lat;
        var finalDestLon = _nodes[destinationNodeIndex].Lon;
        var distancesRented = ArrayPool<float>.Shared.Rent(_nodes.Count);
        Array.Clear(distancesRented);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        float GetDistance(float* distances, int originIndex)
        {
            if (distances[originIndex] <= 0)
            {
                distances[originIndex] = ComputeDistance(_nodes[originIndex].Lat, _nodes[originIndex].Lon, finalDestLat, finalDestLon);
            }
            return distances[originIndex];
        }
        try
        {
            fixed (int* fp = fastestParent)
            fixed (bool* db = dirtyBits)
            fixed (int* no = _nodeOffset)
            fixed (int* lc = _linkCounts)
            fixed (Link* l = _links)
            fixed (float* distances = distancesRented)
            {
                fp[originNodeIndex] = originNodeIndex;
                db[originNodeIndex / CacheChunkSize] = true;
                foreach (var link in _nodes[originNodeIndex].Connections)
                {
                    toExplore.Push(link.Destination, originNodeIndex, link.Time + GetDistance(distances, link.Destination), link.Time);
                }
                while (toExplore.Count > 0)
                {
                    var current = toExplore.PopMin();
                    // don't explore things that we have already done
                    int currentDestination = current.Destination;
                    // if there was already a faster way to get here continue
                    if (fp[currentDestination] != -1)
                    {
                        continue;
                    }
                    fp[currentDestination] = current.Origin;
                    db[currentDestination / CacheChunkSize] = true;
                    // check to see if we have hit our destination
                    if (currentDestination == destinationNodeIndex)
                    {
                        return GeneratePath(fastestParent, destinationNodeIndex);
                    }
                    // foreach (var childDestination in links)
                    var nodeOffset = no[currentDestination];
                    for (int i = 0; i < lc[currentDestination]; i++)
                    {
                        // explore everything that hasn't been solved, the min heap will update if it is a faster path to the child node
                        if (fp[l[nodeOffset + i].Destination] == -1)
                        {
                            // make sure cars are allowed on the link
                            var linkCost = l[nodeOffset + i].Time;
                            if (linkCost >= 0)
                            {
                                toExplore.Push(l[nodeOffset + i].Destination, currentDestination,
                                    current.CostSoFar + linkCost + GetDistance(distances, l[nodeOffset + i].Destination),
                                    current.CostSoFar + linkCost);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(distancesRented, false);
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
            if (next != prev)
            {
                ret.Add((next, prev));
                break;
            }
            prev = next;
        }
        // reverse the list before returning it
        ret.Reverse();
        return ret;
    }

    internal (int[] fastestPath, bool[] dirtyBits) GetCache()
    {
        var fp = new int[_nodes.Count];
        var dirty = new bool[(int)Math.Ceiling((float)_nodes.Count / CacheChunkSize)];
        Array.Fill(fp, -1);
        Array.Fill(dirty, false);
        return (fp, dirty);
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
internal record struct Link(int Destination, float Time);

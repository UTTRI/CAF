using System.Runtime.CompilerServices;


namespace ProcessOpenStreetMap;

internal sealed class Network
{
    private readonly List<Node> _nodes;
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
    }

    private bool HasCachedVersion(FileInfo requestedFile)
    {
        return File.Exists(GetCachedName(requestedFile));
    }

    private string GetCachedName(FileInfo requestedFile) => requestedFile.FullName + ".cached";

    private List<Node> LoadCachedVersion(FileInfo requestedFile)
    {
        using var reader = new BinaryReader(File.OpenRead(GetCachedName(requestedFile)));
        var magicNumber = reader.ReadInt64();
        if(magicNumber != 6473891447)
        {
            throw new Exception("Invalid Magic Number!");
        }
        var numberOfNodes = reader.ReadInt32();
        var numberOfLinks = new int[numberOfNodes];
        var ret = new List<Node>(numberOfNodes);
        for (int i = 0; i < numberOfNodes; i++)
        {
            var lat = reader.ReadSingle();
            var lon = reader.ReadSingle();
            numberOfLinks[i] = reader.ReadInt32();
            ret.Add(new Node(lat, lon, new List<Link>(numberOfLinks[i])));
        }
        for (int i = 0; i < numberOfNodes; i++)
        {
            for(int j = 0; j < numberOfLinks[i]; j++)
            {
                var destination = reader.ReadInt32();
                var time = reader.ReadSingle();
                ret[i].Connections.Add(new Link(i, j, time));
            }
        }
        return ret;
    }

    private void SaveCachedVersion(FileInfo requestedFile)
    {
        using var writer = new BinaryWriter(File.OpenWrite(GetCachedName(requestedFile)));
        // magic number
        writer.Write(6473891447L);
        writer.Write(_nodes.Count);
        for(int i = 0; i < _nodes.Count; i++)
        {
            writer.Write(_nodes[i].Lat);
            writer.Write(_nodes[i].Lon);
            writer.Write(_nodes[i].Connections.Count);
        }
        for (int i = 0; i < _nodes.Count; i++)
        {
            for(int j = 0; j < _nodes[i].Connections.Count; j++)
            {
                writer.Write(_nodes[i].Connections[j].Destination);
                writer.Write(_nodes[i].Connections[j].Time);
            }
        }
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

    public (float time, float distance) Compute(float originX, float originY, float destinationX, float destinationY)
    {
        // Find closest origin node in the network
        int originNodeIndex = FindClosestNodeIndex(originX, originY);
        // Find closest destination node in the network
        int destinationNodeIndex = FindClosestNodeIndex(destinationX, destinationY);
        // Find the fastest route between the two points
        var path = GetFastestPath(originNodeIndex, destinationNodeIndex);
        if(path is null)
        {
            return (-1, -1);
        }
        // Compute the travel time and distance for the fastest path
        var distance = 0.0f;
        var time = 0.0f;
        for(int i = 0; i < path.Count; i++)
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

    /// <summary>
    /// Finds the closest node to the given coordinates.
    /// </summary>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <returns>The index of the node that is the closest.</returns>
    private int FindClosestNodeIndex(float lat, float lon)
    {
        int min = -1;
        float minDistancce = float.PositiveInfinity;
        for (int i = 0; i < _nodes.Count; i++)
        {
            var distance = ComputeDistance(lat, lon, _nodes[i].Lat, _nodes[i].Lon);
            if(distance < minDistancce)
            {
                min = i;
                minDistancce = distance;
            }
        }
        if(min == -1)
        {
            Console.WriteLine("No node found!");
        }
        return min;
    }

    /// <summary>
    /// Thread-safe on a static network
    /// </summary>
    /// <param name="originNodeIndex"></param>
    /// <param name="destinationNodeIndex"></param>
    /// <returns></returns>
    public List<(int origin, int destination)>? GetFastestPath(int originNodeIndex, int destinationNodeIndex)
    {
        var fastestParent = new Dictionary<(int origin, int destination), (int parentOrigin, int parentDestination)>();
        MinHeap toExplore = new();
        foreach (var link in _nodes[originNodeIndex].Connections)
        {
            toExplore.Push((originNodeIndex, link.Destination), (-1, originNodeIndex), link.Time);
        }
        while (toExplore.Count > 0)
        {
            var current = toExplore.PopMin();
            // don't explore things that we have already done                
            if (!fastestParent.TryAdd(current.link, current.parentLink))
            {
                // check to see if there are some turns that were restricted that need to be explored
                continue;
            }
            // check to see if we have hit our destination
            int currentDestination = current.link.destination;
            if (currentDestination == destinationNodeIndex)
            {
                return GeneratePath(fastestParent, current);
            }
            var node = _nodes[currentDestination];
            var links = node.Connections;
            foreach (var childDestination in links)
            {
                // explore everything that hasn't been solved, the min heap will update if it is a faster path to the child node
                (int currentDestination, int childDestination) nextStep = (currentDestination, childDestination.Destination);
                if (!fastestParent.ContainsKey(nextStep))
                {
                    // don't explore centroids that are not our destination
                    if (childDestination.Destination != destinationNodeIndex)
                    {
                        // ensure there is not a turn restriction
                        //if (!_turnRestrictions.Contains((current.link.origin, currentDestination, childDestination)))
                        {
                            // make sure cars are allowed on the link
                            var linkCost = childDestination.Time;
                            if (linkCost >= 0)
                            {
                                toExplore.Push(nextStep, current.link, current.cost + linkCost);
                            }
                        }
                    }
                }
            }
        }
        return null;
    }
    private static List<(int origin, int destination)> GeneratePath(Dictionary<(int origin, int destination), (int parentOrigin, int parentDestination)> fastestParent,
            ((int origin, int destination) link, (int parentOrigin, int parentDestination) parentLink, float cost) current)
    {
        // unwind the parents to build the path
        var ret = new List<(int, int)>();
        var cIndex = current.parentLink;
        ret.Add(current.link);
        if (cIndex.parentOrigin >= 0)
        {
            ret.Add(cIndex);
            while (true)
            {
                if (fastestParent.TryGetValue(cIndex, out var parent))
                {
                    if (parent.parentOrigin >= 0)
                    {
                        ret.Add((cIndex = parent));
                        continue;
                    }
                }
                break;
            }
            // reverse the list before returning it
            ret.Reverse();
        }
        return ret;
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
/// <param name="Origin"></param>
/// <param name="Destination"></param>
/// <param name="Time"></param>
internal record struct Link(int Origin, int Destination, float Time);

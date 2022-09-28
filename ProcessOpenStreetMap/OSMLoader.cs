using OsmSharp;
using OsmSharp.API;
using OsmSharp.Streams;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ProcessOpenStreetMap;

internal static class OSMLoader
{
    internal static List<Node> LoadOSMNetwork(string fileName)
    {
        List<Node> networkNodes = new();
        Dictionary<long, int> nodeLookup = new();

        void StoreNode(OsmSharp.Node node)
        {
            if (node.Id is null)
            {
                ThrowNoId();
            }
            networkNodes.Add(ConvertNode(node));
            nodeLookup[(long)node.Id] = networkNodes.Count - 1;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float GetTravelTime(float distance, HighwayType type)
        {
            static float GetSpeedInKMPerMinute(HighwayType type)
            {
                const float freewaySpeed = 100.0f / 60.0f;
                const float highwaySpeed = 80.0f / 60.0f;
                const float arterialSpeed = 60.0f / 60.0f;
                const float defaultSpeed = 40.0f / 60.0f;
                return type switch
                {
                    HighwayType.Trunk => freewaySpeed,
                    HighwayType.TrunkLink => freewaySpeed,
                    HighwayType.Motorway => freewaySpeed,
                    HighwayType.MotorwayLink => freewaySpeed,
                    HighwayType.Primary => highwaySpeed,
                    HighwayType.PrimaryLink => highwaySpeed,
                    HighwayType.Secondary => arterialSpeed,
                    HighwayType.SecondaryLink => arterialSpeed,
                    HighwayType.Tertiary => arterialSpeed,
                    HighwayType.TertiaryLink => arterialSpeed,
                    _ => defaultSpeed
                };
            }
            return GetSpeedInKMPerMinute(type) * distance;
        }

        void StoreLink(long first, long second, HighwayType type)
        {
            var originIndex = nodeLookup[first];
            var origin = networkNodes[originIndex];
            var destinationIndex = nodeLookup[second];
            var destination = networkNodes[destinationIndex];
            var time = GetTravelTime(Network.ComputeDistance(origin.Lat, origin.Lon, destination.Lat, destination.Lon), type);
            origin.Connections.Add(new Link(destinationIndex, time));
        }

        HashSet<long> nodesInWays = new();
        using var stream = new OsmSharp.Streams.XmlOsmStreamSource(File.OpenRead(fileName));
        // Get all node numbers that are part of a street
        Console.WriteLine("Searching OSM for all nodes that are connected in a way.");
        foreach (var entry in stream)
        {
            if (entry.Type == OsmGeoType.Way && entry is OsmSharp.Way way)
            {
                HighwayType type = HighwayType.NotRoad;
                if (way.Tags is not null)
                {
                    foreach (var tag in way.Tags)
                    {
                        if (tag.Key is not null && tag.Value is not null)
                        {
                            if (tag.Key == "highway")
                            {
                                type = GetHighwayClass(tag.Value);
                            }
                        }
                    }
                }
                if (type != HighwayType.NotRoad)
                {
                    foreach (var containedNode in way.Nodes)
                    {
                        // It is fine to ignore if a node has been
                        // already been added to skip
                        _ = nodesInWays.Add(containedNode);
                    }
                }
            }
        }
        // Now get the points for each node
        stream.Reset();
        Console.WriteLine("Storing all nodes that were identified as being in a way.");
        foreach (var entry in stream)
        {
            if (entry.Type == OsmGeoType.Node && entry is OsmSharp.Node n)
            {
                var id = n.Id;
                if (id is not null && nodesInWays.Contains((long)id))
                {
                    StoreNode(n);
                }
            }
        }
        // Now finally construct all of the links
        stream.Reset();
        Console.WriteLine("Creating links for all ways.");
        foreach (var entry in stream)
        {
            if (entry.Type == OsmGeoType.Way && entry is Way way)
            {
                long prev = -1;
                HighwayType type = HighwayType.NotRoad;
                bool oneWay = false;
                if (way.Tags is not null)
                {
                    foreach (var tag in way.Tags)
                    {
                        if (tag.Key is not null && tag.Value is not null)
                        {
                            if (tag.Key == "highway")
                            {
                                type = GetHighwayClass(tag.Value);
                            }
                            if (tag.Key.Equals("oneway", StringComparison.InvariantCultureIgnoreCase)
                                && tag.Value.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                            {
                                oneWay = true;
                            }
                        }
                    }
                }
                if (type != HighwayType.NotRoad)
                {
                    foreach (var node in way.Nodes)
                    {
                        if (prev >= 0)
                        {
                            StoreLink(prev, node, type);
                            if (!oneWay)
                            {
                                StoreLink(node, prev, type);
                            }
                        }
                        prev = node;
                    }
                }
            }
        }
        return networkNodes;
    }

    // https://wiki.openstreetmap.org/wiki/Key:highway
    private static HighwayType GetHighwayClass(string value)
    {
        return value switch
        {
            "motorway" => HighwayType.Motorway,
            "motorway_link" => HighwayType.MotorwayLink,
            "trunk" => HighwayType.Trunk,
            "trunk_link" => HighwayType.Trunk,
            "primary" => HighwayType.Primary,
            "secondary" => HighwayType.Secondary,
            "tertiary" => HighwayType.Tertiary,
            "residential" => HighwayType.Residential,
            "primary_link" => HighwayType.PrimaryLink,
            "secondary_link" => HighwayType.SecondaryLink,
            "tertiary_link" => HighwayType.TertiaryLink,
            "road" => HighwayType.Road,
            _ => HighwayType.NotRoad
        };
    }

    private static Node ConvertNode(OsmSharp.Node node)
    {
        if (node.Latitude is double lat && node.Longitude is double lon)
        {
            return new Node((float)lat, (float)lon, new List<Link>());
        }
        ThrowNoId();
        return default;
    }

    [DoesNotReturn]
    private static void ThrowNoId()
    {
        throw new Exception("We encountered a node with no id!");
    }
}

enum HighwayType
{
    NotRoad = 0,
    Motorway,
    MotorwayLink,
    Trunk,
    TrunkLink,
    Primary,
    PrimaryLink,
    Secondary,
    SecondaryLink,
    Tertiary,
    TertiaryLink,
    Residential,
    Road,
}
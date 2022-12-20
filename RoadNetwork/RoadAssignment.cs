using static System.Collections.Specialized.BitVector32;

namespace RoadNetwork;

/// <summary>
/// This class is used to generate congested times on the transit network
/// </summary>
public static class RoadAssignment
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="network"></param>
    /// <param name="zoneSystem"></param>
    /// <param name="demand"></param>
    /// <returns></returns>
    public static float[] ApplyDemandToNetwork(Network network, ZoneSystem zoneSystem, Matrix demand)
    {
        const int numberOfIterations = 100;
        const float relativeGap = 0.001f;
        RoadPaths paths = new(zoneSystem);
        float[] linkVolumes = new float[network.LinkCount];
        var freeflowTimes = network.GetTimes();
        for (int i = 0; i < numberOfIterations; i++)
        {
            UpdateRoadPaths(zoneSystem, network, paths);
            var gap = UpdateDemandOnLink(zoneSystem, paths, linkVolumes, i, demand);
            ComputeUpdatedTravelTimes(network, linkVolumes, freeflowTimes);

            // If we have satisfied the gap, we can terminate.
            if ((i > 0) && (gap < relativeGap))
            {
                break;
            }
            paths.ClearPaths();
        }
        return linkVolumes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="zoneSystem"></param>
    /// <param name="network"></param>
    /// <param name="paths"></param>
    private static void UpdateRoadPaths(ZoneSystem zoneSystem, Network network, RoadPaths paths)
    {
        Parallel.For(0, zoneSystem.Length,
            () => network.GetCache(),
            (originIndex, _, cache) =>
            {
                var originNode = zoneSystem.GetNodeForZoneIndex(originIndex);
                for (int j = 0; j < zoneSystem.Length; j++)
                {
                    var destinationNode = zoneSystem.GetNodeForZoneIndex(j);
                    var path = network.GetFastestPathDijkstra(originNode, destinationNode, cache.fastestPath, cache.dirtyBits);
                    var resultPath = paths.GetPath(originIndex, j);
                    if (path is null || path.Count <= 0)
                    {
                        return cache;
                    }
                    resultPath.Add(path[0].origin);
                    foreach (var (_, destination) in path)
                    {
                        resultPath.Add(destination);
                    }
                }
                return cache;
            }, (cache) =>
            {
                // do nothing for aggregation
            }
        );
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="network"></param>
    /// <param name="paths"></param>
    /// <param name="linkVolumes"></param>
    /// <returns></returns>
    private static float UpdateDemandOnLink(ZoneSystem zoneSystem, RoadPaths paths, float[] linkVolumes, int iterationNumber, Matrix demand)
    {
        var alpha = 2 / (iterationNumber + 2);
        const int chunkSize = 32;
        int numberOfChunks = (int)Math.Ceiling((double)zoneSystem.Length / (double)chunkSize);
        float[][] innerLinkVolumes = new float[numberOfChunks][];
        Parallel.For(0, numberOfChunks,
            (int chunkIndex) =>
            {
                int endIndex = Math.Min(chunkIndex * (chunkSize + 1), zoneSystem.Length);
                innerLinkVolumes[chunkIndex] = new float[linkVolumes.Length];
                float[] linkVolumeRow = innerLinkVolumes[chunkIndex];
                for (int i = chunkIndex * chunkSize; i < endIndex; i++)
                {
                    for (int j = 0; j < zoneSystem.Length; j++)
                    {
                        var demandToAdd = demand.Data[i * zoneSystem.Length + j];
                        var path = paths.GetPath(i, j);
                        for (int k = 0; k < path.Count; k++)
                        {
                            linkVolumeRow[path[k]] = demandToAdd * alpha + linkVolumeRow[path[k]] * (1 - alpha);
                        }
                    }
                }
            }
        );
        var maxRelativeDifference = float.NegativeInfinity;
        // Update the links and compute the relative gap
        for (int j = 0; j < linkVolumes.Length; j++)
        {
            var oldValue = linkVolumes[j];
            linkVolumes[j] = 0;
            for (int i = 0; i < innerLinkVolumes.Length; i++)
            {
                linkVolumes[j] += innerLinkVolumes[i][j];
            }
            maxRelativeDifference = Math.Max(maxRelativeDifference, ((linkVolumes[j] - oldValue) / oldValue));
        }
        return maxRelativeDifference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="network"></param>
    /// <param name="paths"></param>
    /// <param name="linkVolumes"></param>
    private static void ComputeUpdatedTravelTimes(Network network, float[] linkVolumes, float[] freeFlowTimes)
    {
        network.UpdateLinkTravelTimes(linkVolumes, freeFlowTimes);
    }
}

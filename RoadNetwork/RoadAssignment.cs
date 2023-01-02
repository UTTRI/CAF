namespace RoadNetwork;

/// <summary>
/// This class is used to generate congested times on the transit network
/// </summary>
public static class RoadAssignment
{
    /// <summary>
    /// The grouping size of zones that each thread will process the results for
    /// </summary>
    private const int ChunkSize = 32;

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
            var stepSize = i == 0 ? 1.0f : FindStepSize(paths, demand);
            UpdateLinks(network, zoneSystem, demand, paths, linkVolumes, freeflowTimes, stepSize);
            var rgap = paths.ComputeRelativeGap(network, demand);
            // If we have satisfied the gap, we can terminate.
            if ((i > 0) && (rgap < relativeGap))
            {
                break;
            }
            paths.UpdateForNextIteration();
        }
        return linkVolumes;
    }

    private static void UpdateLinks(Network network, ZoneSystem zoneSystem, Matrix demand, RoadPaths paths, float[] linkVolumes, float[] freeflowTimes, float stepSize)
    {
        UpdateDemandOnLink(zoneSystem, paths, linkVolumes, demand, stepSize);
        ComputeUpdatedLinkTravelTimes(network, linkVolumes, freeflowTimes);
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
                    var path = network.GetFastestPathDijkstra((int)originNode, (int)destinationNode, cache.fastestPath, cache.dirtyBits);
                    var resultPath = paths.GetPath(originIndex, j);
                    if (path is null || path.Count <= 0)
                    {
                        paths.GetCost(originIndex, j) = 0;
                        return cache;
                    }
                    // Build our result path and get the travel time
                    // before we update the road volumes
                    paths.GetCost(originIndex, j) = network.GetTravelTime(path);
                    resultPath.Add(path[0].origin);
                    foreach (var (_, destination) in path)
                    {
                        resultPath.Add(destination);
                    }
                }
                return cache;
            },
            DoNothing
        );
    }

    /// <summary>
    /// Find the step size to use, only call this
    /// after the first iteration has completed.
    /// </summary>
    /// <param name="paths">The path data</param>
    /// <param name="demand">The demand matrix for the assignment</param>
    /// <returns>The step size to apply to the network</returns>
    private static float FindStepSize(RoadPaths paths, Matrix demand)
    {
        var min = 0.0f;
        var max = 1.0f;
        float current = 0.5f;
        // This comes to 10 iterations since the space shrinks by half each time
        const float epsilon = 0.001f;
        while(max - min > epsilon)
        {
            current = 0.5f * (min + max);
            if(paths.SumFirstDerivative(demand, current) > 0)
            {
                min = current;
            }
            else
            {
                max = current;
            }
        }
        return current;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="network"></param>
    /// <param name="paths"></param>
    /// <param name="linkVolumes"></param>
    /// <returns></returns>
    private static void UpdateDemandOnLink(ZoneSystem zoneSystem, RoadPaths paths, float[] linkVolumes, Matrix demand, float alpha)
    {
        int numberOfChunks = (int)Math.Ceiling((double)zoneSystem.Length / (double)ChunkSize);
        float[][] innerLinkVolumes = new float[numberOfChunks][];
        Parallel.For(0, numberOfChunks,
            (int chunkIndex) =>
            {
                int endIndex = Math.Min(chunkIndex * (ChunkSize + 1), zoneSystem.Length);
                innerLinkVolumes[chunkIndex] = new float[linkVolumes.Length];
                float[] linkVolumeRow = innerLinkVolumes[chunkIndex];
                for (int i = chunkIndex * ChunkSize; i < endIndex; i++)
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
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="network"></param>
    /// <param name="paths"></param>
    /// <param name="linkVolumes"></param>
    private static void ComputeUpdatedLinkTravelTimes(Network network, float[] linkVolumes, float[] freeFlowTimes)
    {
        network.UpdateLinkTravelTimes(linkVolumes, freeFlowTimes);
    }

    /// <summary>
    /// Use this for a delegate call that needs to consume something and then do nothing
    /// </summary>
    private static void DoNothing<T>(T _) {}

    public static Matrix GetTravelTimes(Network network, ZoneSystem zoneSystem)
    {
        var ret = new Matrix(zoneSystem.Length);
        Parallel.For(0, zoneSystem.Length,
            () => network.GetCache(),
            (i, _, cache) =>
            {
                var originNode = zoneSystem.GetNodeForZoneIndex(i);
                for (int j = 0; j < zoneSystem.Length; j++)
                {
                    var destinationNode = (int)zoneSystem.GetNodeForZoneIndex(j);
                    var path = network.GetFastestPathDijkstra((int)originNode, (int)destinationNode, cache.fastestPath, cache.dirtyBits);
                    ret.Data[i * zoneSystem.Length + j] = network.GetTravelTime(path);
                }
                return cache;
            },
            DoNothing
        );
        return ret;
    }
}

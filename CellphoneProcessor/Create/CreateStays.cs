using CellphoneProcessor.Utilities;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using RoadNetwork;
using System.Collections.Concurrent;

namespace CellphoneProcessor.Create;

/// <summary>
/// This class is designed to provide processing for Stays.
/// </summary>
internal static class CreateStays
{
    internal static Task RunAsync(string chunkFolder, string shapeFile, string tazColumn, string roadNetworkFile, string outputFile,
        ProgressUpdate chunkingProgress, ProgressUpdate tazProgress, ProgressUpdate roadProgress, ProgressUpdate writingProgress)
    {
        return Task.Run(() =>
        {
            DirectoryInfo[] chunkFolders = GetChunksFolders(chunkFolder);
            InitializeProgress(chunkingProgress, chunkFolders);
            ConcurrentDictionary<string, List<ProcessedRecord>> records = new();
            // Load in the zone system
            var zonePolygons = ShapefileHelper.ReadShapeFile(shapeFile, tazColumn);
            // Load the road network
            var roadNetwork = new Network(roadNetworkFile);
            // Process the records iterating through each day
            for (int i = 0; i < chunkFolders.Length; i++)
            {
                var dir = chunkFolders[i].FullName;
                CreateClusters(dir, records);
                // For each of the clusters go through and 
                // Now that we are done processing the day increment the progress
                chunkingProgress.Current = i + 1;
            }
            tazProgress.Total = records.Count;
            roadProgress.Total = records.Count;
            // Generate TAZ
            UpdateWithTAZ(zonePolygons, records, tazProgress);
            // Generate Road Times
            GenerateRoadTimes(roadNetwork, records, roadProgress);
            // Write Records            
            WriteRecords(records, outputFile, writingProgress);
        });
    }


    private static void InitializeProgress(ProgressUpdate progress, DirectoryInfo[] chunkFolders)
    {
        // Process chunks, Road Network Times, TAZ, Write Records
        var total = chunkFolders.Length;
        progress.Current = 0;
        progress.Total = total;
    }

    private static DirectoryInfo[] GetChunksFolders(string chunkFolder)
    {
        const string chunkFolderSearch = "Day*";
        DirectoryInfo dir = new(chunkFolder);
        // Get a list of the directories to process sorted by dayNumber
        // Ignore the case where someone really wants to have days that overlap.
        var ret =
            dir.GetDirectories(chunkFolderSearch)
            .Select(dir =>
            {
                if (!int.TryParse(dir.Name.AsSpan()[3..], out int dayNumber))
                {
                    dayNumber = -1;
                }
                return (dayNumber, dir);
            })
            .Where(record => record.dayNumber > 0)
            .OrderBy(record => record.dayNumber)
            .Select(record => record.dir)
            .ToArray();
        return ret;
    }

    private static void CreateClusters(string directoryName, ConcurrentDictionary<string, List<ProcessedRecord>> records)
    {
        var allDevices = ChunkEntry.LoadOrderedChunks(directoryName);
        Parallel.ForEach(Enumerable.Range(0, allDevices.Length),
            (deviceIndex, _, cache) =>
            {
                var device = allDevices[deviceIndex];
                static ProcessedRecord CreateRecord(ChunkEntry entry)
                {
                    return new ProcessedRecord(entry.DeviceID, entry.Lat, entry.Long, entry.HAccuracy, entry.TS, entry.TS,
                        float.NaN, float.NaN, float.NaN, HighwayType.NotRoad, HighwayType.NotRoad, 1, -1);
                }

                int firstIndex;
                List<ProcessedRecord>? processedRecords;
                ProcessedRecord current;

                void PopPreviousToCurrent()
                {
                    current = processedRecords![^1];
                    processedRecords.RemoveAt(processedRecords.Count - 1);
                }

                // Get the device's previous records, if no entry exists create one
                if (!records.TryGetValue(device[0].DeviceID, out processedRecords))
                {
                    records[device[0].DeviceID] = processedRecords = new List<ProcessedRecord>();
                    firstIndex = 1;
                    current = CreateRecord(device[0]);
                }
                else
                {
                    // If we have previous records, pop the last one off the stack and continue
                    PopPreviousToCurrent();
                    firstIndex = 0;
                }

                void UpdateCurrent(ChunkEntry entry)
                {
                    var entries = current.NumberOfPings;
                    var y = ((current.Lat * (entries - 1)) + entry.Lat) / entries;
                    var x = ((current.Long * (entries - 1)) + entry.Long) / entries;
                    current = current with
                    {
                        Lat = y,
                        Long = x,
                        EndTS = entry.TS,
                        NumberOfPings = current.NumberOfPings + 1,
                    };
                }

                const float distanceThreshold = 0.1f;
                for (int i = firstIndex; i < device.Length; i++)
                {
                    var straightLineDistance = Network.ComputeDistance(current.Lat, current.Long, device[i].Lat, device[i].Long);
                    var deltaTime = ComputeDuration(current.EndTS, device[i].TS);
                    var speed = straightLineDistance / deltaTime;
                    // Sanity check the record
                    if (speed > 120.0f)
                    {
                        continue;
                    }
                    // If we are in a "new location" add an entry.
                    if (straightLineDistance > distanceThreshold)
                    {
                        // Check the stay duration if greater than 15 minutes
                        if (ComputeDuration(current.StartTS, current.EndTS) < 0.25f)
                        {
                            // If the record was not long enough, pop back to the previous good entry
                            if (processedRecords.Count > 0
                                && Network.ComputeDistance(processedRecords[^1].Lat, processedRecords[^1].Long, device[i].Lat, device[i].Long) <= distanceThreshold)
                            {
                                // If this ping is close enough to the previously good entry, update it
                                PopPreviousToCurrent();
                                UpdateCurrent(device[i]);
                            }
                            else
                            {
                                // If there is no previous record to fall back to, or we are too far away from the previous one, use this entry
                                current = CreateRecord(device[i]);
                            }
                        }
                        else
                        {
                            // If the current record is long enough add it as a good record
                            processedRecords.Add(current);
                            current = CreateRecord(device[i]);
                        }
                    }
                    else
                    {
                        // If the distance was small enough add it to the cluster
                        UpdateCurrent(device[i]);
                    }
                }

                // Add the currently processed entry back onto the stack of things so it is available for a future day
                processedRecords.Add(current);
            }
        );
    }

    private static void UpdateWithTAZ((Polygon[] ZoneSystem, int[] TAZ) zonePolygons, ConcurrentDictionary<string, List<ProcessedRecord>> records,
        ProgressUpdate progress)
    {
        IndexedPointInAreaLocator[] zoneSystem = zonePolygons.ZoneSystem.Select(z => new IndexedPointInAreaLocator(z)).ToArray();
        progress.Total = records.Count;
        long processed = 0;
        int IndexOfCollision(float x, float y)
        {
            // For some reason the map has lat and long inverted
            var point = new Coordinate(y, x);
            for (int i = 0; i < zoneSystem.Length; i++)
            {
                var test = zoneSystem[i].Locate(point);
                if (!test.HasFlag(Location.Exterior))
                {
                    return i;
                }
            }
            return -1;
        }

        Parallel.ForEach(records.Values, deviceRecords =>
        {
            for (int i = 0; i < deviceRecords.Count; i++)
            {
                var index = IndexOfCollision(deviceRecords[i].Long, deviceRecords[i].Lat);
                if (index >= 0)
                {
                    deviceRecords[i] = deviceRecords[i] with
                    {
                        TAZ = zonePolygons.TAZ[index]
                    };
                }
            }
            Interlocked.Increment(ref processed);
            if(processed % 1000 == 0)
            {
                progress.Current = processed;
            }
        });
        progress.Current = progress.Total;
    }

    /// <summary>
    /// Generates the road times for each record
    /// </summary>
    /// <param name="network"></param>
    /// <param name="records"></param>
    private static void GenerateRoadTimes(Network network, ConcurrentDictionary<string, List<ProcessedRecord>> records,
        ProgressUpdate progress)
    {
        progress.Total = records.Count;
        long processed = 0;
        Parallel.ForEach(records.Values, () =>
        {
            return network.GetCache();
        },
        (List<ProcessedRecord> entries, ParallelLoopState _, (int[] fastestPath, bool[] dirtyBits) cache) =>
        {
            if (entries.Count == 0)
            {
                Interlocked.Increment(ref processed);
                return cache;
            }
            // Test to make sure the last entry is long enough
            if (ComputeDuration(entries[^1].StartTS, entries[^1].EndTS) < 0.25f)
            {
                // If the entry is under 15 minutes, then remove it before processing times
                entries.RemoveAt(entries.Count - 1);
            }
            for (int i = 1; i < entries.Count; i++)
            {
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(entries[i - 1].Lat, entries[i - 1].Long,
                            entries[i].Lat, entries[i].Long, cache.fastestPath, cache.dirtyBits);
                var straightLine = Network.ComputeDistance(entries[i - 1].Lat, entries[i - 1].Long,
                    entries[i].Lat, entries[i].Long);
                entries[i] = entries[i] with
                {
                    TravelTime = time,
                    RoadDistance = distance,
                    OriginRoadType = originRoadType,
                    DestinationRoadType = destinationRoadType,
                    Distance = straightLine,
                };
            }
            Interlocked.Increment(ref processed);
            if (processed % 1000 == 0)
            {
                progress.Current = processed;
            }
            return cache;
        },
        (_) => { } // do nothing
        );
        progress.Current = progress.Total;
    }

    private static void WriteRecords(ConcurrentDictionary<string, List<ProcessedRecord>> records, string outputFile,
        ProgressUpdate progress)
    {
        using var writer = new StreamWriter(outputFile);
        long processed = 0;
        progress.Total = records.Count;
        writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
        foreach (var device in records
        .OrderBy(dev => dev.Key)
    )
        {
            foreach (var entry in device.Value)
            {
                writer.Write(entry.DeviceID);
                writer.Write(',');
                writer.Write(entry.Lat);
                writer.Write(',');
                writer.Write(entry.Long);
                writer.Write(',');
                writer.Write(entry.HAccuracy);
                writer.Write(',');
                writer.Write(entry.StartTS);
                writer.Write(',');
                writer.Write(entry.EndTS);
                writer.Write(',');
                writer.Write(entry.TravelTime);
                writer.Write(',');
                writer.Write(entry.RoadDistance);
                writer.Write(',');
                writer.Write(entry.Distance);
                writer.Write(',');
                writer.Write(entry.NumberOfPings);
                writer.Write(',');
                writer.Write((int)entry.OriginRoadType);
                writer.Write(',');
                writer.Write((int)entry.DestinationRoadType);
                writer.Write(',');
                writer.WriteLine((int)entry.TAZ);
                processed++;
                if(processed % 1000 == 0)
                {
                    progress.Current = processed;
                }
            }
        }
        progress.Current = progress.Total;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="startTS"></param>
    /// <param name="endTS"></param>
    /// <returns></returns>
    private static float ComputeDuration(long startTS, long endTS)
    {
        return (endTS - startTS) / 3600.0f;
    }

    record struct ProcessedRecord(string DeviceID, float Lat, float Long, float HAccuracy, long StartTS, long EndTS, float TravelTime, float RoadDistance, float Distance,
       HighwayType OriginRoadType, HighwayType DestinationRoadType, int NumberOfPings, int TAZ);
}

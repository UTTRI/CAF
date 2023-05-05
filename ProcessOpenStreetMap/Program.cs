using ProcessOpenStreetMap;
using RoadNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Timers;

// Get the network file path and the root directory.
var arguments = Environment.GetCommandLineArgs();
string rootDirectory;
string networkFilePath;

if (arguments == null || arguments.Length <= 1)
{
    networkFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Bogota.osmx";
    rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days";

}
else if (arguments.Length == 3)
{
    networkFilePath = arguments[1];
    rootDirectory = arguments[2];
}
else
{
    Console.WriteLine("USAGE: [NetworkFilePath] [RootDirectory]");
    System.Environment.Exit(0);
    return;
}

// 

var year = 2019;
var month = 9;

Console.WriteLine("Loading road network...");

Network network = new(networkFilePath);
ConcurrentDictionary<string, List<ProcessedRecord>> records = new();

void ProcessRoadtimes(string directoryName, int day, bool isTheLastDay)
{
    Console.WriteLine("Loading Chunks...");
    var allDevices = ChunkEntry.LoadOrderedChunks(directoryName);
    Console.WriteLine("Finished loading Entries...");
    int processedDevices = 0;
    Console.WriteLine("Starting to process entries.");
    var watch = Stopwatch.StartNew();
    var totalEntries = allDevices.Sum(dev => dev.Length);
    List<ProcessedRecord> processedRecords = new(totalEntries);
    Parallel.ForEach(Enumerable.Range(0, allDevices.Length),
        (deviceIndex, _, cache) =>
        {
            var device = allDevices[deviceIndex];
            static ProcessedRecord CreateRecord(ChunkEntry entry)
            {
                return new ProcessedRecord(entry.DeviceID, entry.Lat, entry.Long, entry.HAccuracy, entry.TS, entry.TS,
                    float.NaN, float.NaN, float.NaN, HighwayType.NotRoad, HighwayType.NotRoad, 1);
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

            var p = Interlocked.Increment(ref processedDevices);
            if (p % 1000 == 0)
            {
                var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allDevices.Length - p));
                Console.Write($"Processing {p} of {allDevices.Length}, Estimated time remaining: " +
                    $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
            }
        }
    );
    watch.Stop();
    Console.WriteLine($"\nTotal runtime for entries: {watch.ElapsedMilliseconds}ms");
}

var numberOfDaysInMonth = DateTime.DaysInMonth(year, month);

for (int i = 1; i <= numberOfDaysInMonth; i++)
{
    var directory = Path.Combine(rootDirectory, $"Day{i}");
    Console.WriteLine($"Starting to process {directory}");
    ProcessRoadtimes(directory, i, i == numberOfDaysInMonth);
}

Console.WriteLine("Computing distances and travel");

Parallel.ForEach(records.Values, () =>
    {
        return network.GetCache();
    },
    (List<ProcessedRecord> entries, ParallelLoopState _, (int[] fastestPath, bool[] dirtyBits) cache) =>
    {
        if (entries.Count == 0)
        {
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
        return cache;
    },
    (_) => { } // do nothing
);

Console.WriteLine("Writing results to file");

using var writer = new StreamWriter(Path.Combine(rootDirectory, $"ProcessedRoadTimes.csv"));
writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
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
        writer.WriteLine((int)entry.DestinationRoadType);
    }
}

Console.WriteLine("Complete");

static float ComputeDuration(long startTS, long endTS)
{
    return (endTS - startTS) / 3600.0f;
}

record struct ProcessedRecord(string DeviceID, float Lat, float Long, float HAccuracy, long StartTS, long EndTS, float TravelTime, float RoadDistance, float Distance,
   HighwayType OriginRoadType, HighwayType DestinationRoadType, int NumberOfPings);
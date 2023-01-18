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
    networkFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\bogota.osmx";
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


// This dictionary is used to store the last entry that was stored for each device
ConcurrentDictionary<string, LastRecord> lastRecord = new();

void ProcessRoadtimes(string directoryName, int day, bool isTheLastDay)
{
    Console.WriteLine("Loading Chunks...");
    var allDevices = ChunkEntry.LoadOrderedChunks(directoryName);
    Console.WriteLine("Finished loading Entries...");
    int processedDevices = 0;
    int failedPaths = 0;
    Console.WriteLine("Starting to process entries.");
    var watch = Stopwatch.StartNew();
    var totalEntries = allDevices.Sum(dev => dev.Length);
    List<ProcessedRecord> processedRecords = new(totalEntries);
    Parallel.ForEach(Enumerable.Range(0, allDevices.Length),
        () =>
        {
            return (Cache: network.GetCache(), Results: new List<ProcessedRecord>(totalEntries / System.Environment.ProcessorCount));
        },
        (deviceIndex, _, local) =>
        {
            var device = allDevices[deviceIndex];
            var (cache, records) = (local.Cache, local.Results);
            void ProcessEntries(ChunkEntry startingPoint, ChunkEntry entry, int currentIndex, float straightLineDistance)
            {
                records.Add(new ProcessedRecord(entry.DeviceID, startingPoint.Lat, startingPoint.Long, startingPoint.HAccuracy,
                    entry.TS, entry.TS, float.NaN, float.NaN, straightLineDistance, HighwayType.NotRoad, HighwayType.NotRoad, 1));
            }
            void Process(int startingIndex, int currentIndex, float straightLineDistance)
            {
                var startingPoint = device[startingIndex];
                var entry = device[currentIndex];
                ProcessEntries(startingPoint, entry, currentIndex, straightLineDistance);
            }

            var prevX = device[0].Lat;
            var prevY = device[0].Long;
            float currentX = device[0].Lat, currentY = device[0].Long;
            var prevClusterSize = 1;
            var clusterSize = 1;

            int startingIndex = 0;
            var prevStartIndex = 0;
            var firstIndex = 0;

            // Check to see if we have seen this device before.
            // If we have then use its previous position instead of adding a null record.
            if (lastRecord.TryRemove(device[0].DeviceID, out var lastPreviousRecord))
            {
                clusterSize = prevClusterSize = lastPreviousRecord.ClusterSize;
                currentX = prevX = lastPreviousRecord.CurrentX;
                currentY = prevY = lastPreviousRecord.CurrentY;
                // Make sure that the first entry that we process happens after the previous entry
                firstIndex = 0;
                while (firstIndex < device.Length && lastPreviousRecord.PreviousRecord.EndTS > device[firstIndex].TS)
                {
                    firstIndex++;
                }
                // If all of the entries happen after, punt this to the next day and skip the device
                if (firstIndex == device.Length)
                {
                    lastRecord[device[0].DeviceID] = lastPreviousRecord;
                    return (Cache: cache, Results: records);
                }
                records.Add(lastPreviousRecord.PreviousRecord with { DeviceID = device[0].DeviceID });
            }
            else
            {
                records.Add(new ProcessedRecord(device[0].DeviceID, device[0].Lat, device[0].Long, device[0].HAccuracy, device[0].TS, device[0].TS,
                    float.NaN, float.NaN, float.NaN, HighwayType.NotRoad, HighwayType.NotRoad, 1));
                firstIndex = 1;
            }

            var startRecordIndex = records.Count;
            for (int i = firstIndex; i < device.Length; i++)
            {
                const float distanceThreshold = 0.1f;
                var straightLineDistance = Network.ComputeDistance(currentX, currentY, device[i].Lat, device[i].Long);
                var deltaTime = ComputeDuration(records[^1].StartTS, device[i].TS);
                var speed = straightLineDistance / deltaTime;
                // Sanity check the record
                if (speed > 120.0f)
                {
                    continue;
                }
                bool recordGenerated = false;
                // If we are in a "new location" add an entry.
                if (straightLineDistance > distanceThreshold)
                {
                    // Check the stay duration if greater than 15 minutes
                    if (ComputeDuration(records[^1].StartTS, records[^1].EndTS) < 0.25f)
                    {
                        if (startingIndex != 0)
                        {
                            records.RemoveAt(records.Count - 1);
                            startingIndex = prevStartIndex;

                            // Check to see if we jumped back to the previous good cluster.
                            if (Network.ComputeDistance(prevX, prevY, device[i].Lat, device[i].Long) > distanceThreshold)
                            {
                                // The jump from the last good cluster to this point is also large enough for a new record
                                Process(startingIndex, i, straightLineDistance);
                                recordGenerated = true;
                                startingIndex = i;
                                currentX = device[i].Lat;
                                currentY = device[i].Long;
                                clusterSize = 1;
                            }
                            else
                            {
                                // The jump isn't large enough so we should continue the previous good cluster
                                currentX = prevX;
                                currentY = prevY;
                                clusterSize = prevClusterSize;
                            }
                        }
                    }
                    else
                    {
                        // If the previous was good cluster
                        prevStartIndex = startingIndex;
                        Process(startingIndex, i, straightLineDistance);
                        recordGenerated = true;
                        startingIndex = i;
                        // Store the prev state since we know this cluster was good.
                        prevX = currentX;
                        prevY = currentY;
                        prevClusterSize = clusterSize;

                        currentX = device[i].Lat;
                        currentY = device[i].Long;
                        clusterSize = 1;
                    }
                }
                if (!recordGenerated)
                {
                    // If we are not then update the current X,Y
                    var entries = (float)(clusterSize);
                    currentX = (currentX * (entries - 1) + device[i].Lat) / entries;
                    currentY = (currentY * (entries - 1) + device[i].Long) / entries;
                    // Update where this cluster ends
                    clusterSize++;
                    records[^1] = records[^1] with { Lat = currentX, Long = currentY, EndTS = device[i].TS, NumberOfPings = clusterSize };
                }
            }

            // Now that we have all of the records we can start generating the travel episodes between them
            for (int i = startRecordIndex; i < records.Count; i++)
            {
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(records[i - 1].Lat, records[i - 1].Long,
                    records[i].Lat, records[i].Long, cache.fastestPath, cache.dirtyBits);
                if (time < 0)
                {
                    Interlocked.Increment(ref failedPaths);
                }
                records[i] = records[i] with
                {
                    TravelTime = time,
                    RoadDistance = distance,
                    OriginRoadType = originRoadType,
                    DestinationRoadType = destinationRoadType
                };
            }

            // Store the last record for the device and remove it from the queue
            // if this is not the last day.
            // We need to do this after computing the travel times just in case the device has no record
            // for the final day.
            if (!isTheLastDay)
            {
                lastRecord[device[0].DeviceID] = new LastRecord(records[^1], currentX, currentY, clusterSize);
            }

            if (ComputeDuration(records[^1].StartTS, records[^1].EndTS) < 0.25f)
            {
                records.RemoveAt(records.Count - 1);
            }

            var p = Interlocked.Increment(ref processedDevices);
            if (p % 1000 == 0)
            {
                var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allDevices.Length - p));
                Console.Write($"Processing {p} of {allDevices.Length}, Estimated time remaining: " +
                    $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
            }

            return (Cache: cache, Results: records);
        }
    , (local) =>
        {
            lock (processedRecords)
            {
                processedRecords.AddRange(local.Results);
            }
        }
    );
    watch.Stop();
    Console.WriteLine($"\n{failedPaths} were unable to be computed.");
    Console.WriteLine($"Total runtime for entries: {watch.ElapsedMilliseconds}ms");
    Console.WriteLine("Writing Records...");

    // If this is the last day we need to write out the remaining valid clusters which did not have a record in the final day.
    if (isTheLastDay)
    {
        foreach (var key in lastRecord.Keys.ToArray())
        {
            var entry = lastRecord[key];
            var record = entry.PreviousRecord;
            // If the last entry is long enough to count as a stay
            if (ComputeDuration(record.StartTS, record.EndTS) >= 0.25f)
            {
                processedRecords.Add(record);
            }
        }
    }

    var path = Path.Combine(rootDirectory, $"ProcessedRoadTimes.csv");
    using var writer = new StreamWriter(path, day != 1);
    if (day == 1)
    {
        writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
    }
    foreach (var deviceRecords in processedRecords
        .GroupBy(entry => entry.DeviceID, (id, deviceRecords) => (ID: id, Records: deviceRecords.OrderBy(record => record.StartTS)))
        .OrderBy(dev => dev.ID)
        )
    {
        foreach (var entry in deviceRecords.Records)
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
}

var numberOfDaysInMonth = DateTime.DaysInMonth(year, month);

for (int i = 1; i <= numberOfDaysInMonth; i++)
{
    var directory = Path.Combine(rootDirectory, $"Day{i}");
    Console.WriteLine($"Starting to process {directory}");
    ProcessRoadtimes(directory, i, i == numberOfDaysInMonth);
}

Console.WriteLine("Complete");

static float ComputeDuration(long startTS, long endTS)
{
    return (endTS - startTS) / 3600.0f;
}

record ProcessedRecord(string DeviceID, float Lat, float Long, float HAccuracy, long StartTS, long EndTS, float TravelTime, float RoadDistance, float Distance,
    HighwayType OriginRoadType, HighwayType DestinationRoadType, int NumberOfPings);

record LastRecord(ProcessedRecord PreviousRecord, float CurrentX, float CurrentY, int ClusterSize);
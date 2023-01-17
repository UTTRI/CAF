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
ConcurrentDictionary<string, ChunkEntry> lastEntry = new();

void ProcessRoadtimes(string directoryName, int day)
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


            float currentX = device[0].Lat, currentY = device[0].Long;

            void ProcessEntries(ChunkEntry startingPoint, ChunkEntry entry, int currentIndex, float straightLineDistance)
            {
                records.Add(new ProcessedRecord(deviceIndex, currentIndex, currentIndex, float.NaN, float.NaN, straightLineDistance, HighwayType.NotRoad, HighwayType.NotRoad, 1));
            }

            void Process(int startingIndex, int currentIndex, float straightLineDistance)
            {
                var startingPoint = device[startingIndex];
                var entry = device[currentIndex];
                ProcessEntries(startingPoint, entry, currentIndex, straightLineDistance);
            }
            // Check to see if we have seen this device before.
            // If we have then use its previous position instead of adding a null record.
            if (lastEntry.TryGetValue(device[0].DeviceID, out var lastPreviousRecord))
            {
                ProcessEntries(lastPreviousRecord, device[0], 0, Network.ComputeDistance(lastPreviousRecord.Lat, lastPreviousRecord.Long, device[0].Lat, device[0].Long));
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(lastPreviousRecord.Lat, lastPreviousRecord.Long, device[0].Lat,
                    device[0].Long, cache.fastestPath, cache.dirtyBits);
                if (time < 0)
                {
                    Interlocked.Increment(ref failedPaths);
                }
                records[records.Count - 1] = records[records.Count - 1] with
                {
                    TravelTime = time,
                    RoadDistance = distance,
                    OriginRoadType = originRoadType,
                    DestinationRoadType = destinationRoadType
                };
            }
            else
            {
                records.Add(new ProcessedRecord(deviceIndex, 0, 0, float.NaN, float.NaN, float.NaN, HighwayType.NotRoad, HighwayType.NotRoad, 1));
            }
            var startRecordIndex = records.Count;
            int startingIndex = 0;
            var clusterSize = 1;
            float ComputeDuration(int startIndex, int endIndex)
            {
                return (device[endIndex].TS - device[startIndex].TS) / 3600.0f;
            }
            var prevStartIndex = 0;
            var prevX = device[0].Lat;
            var prevY = device[0].Long;
            var prevClusterSize = 1;
            for (int i = 1; i < device.Length; i++)
            {
                const float distanceThreshold = 0.1f;
                var straightLineDistance = Network.ComputeDistance(currentX, currentY, device[i].Lat, device[i].Long);
                var deltaTime = ComputeDuration(records[^1].EndPingIndex, i);
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
                    if (ComputeDuration(startingIndex, i - 1) < 0.25f)
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
                    records[^1] = records[^1] with { EndPingIndex = i, NumberOfPings = clusterSize };
                }
            }

            for (int i = startRecordIndex; i < records.Count; i++)
            {
                var startingPoint = device[records[i - 1].EndPingIndex];
                var endPoint = device[records[i].StartPingIndex];
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(startingPoint.Lat, startingPoint.Long, endPoint.Lat,
                    endPoint.Long, cache.fastestPath, cache.dirtyBits);
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
            lastEntry[device[0].DeviceID] = device[records[^1].EndPingIndex];
            var p = Interlocked.Increment(ref processedDevices);
            if (p % 1000 == 0)
            {
                var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allDevices.Length - p));
                Console.Write($"Processing {p} of {allDevices.Length}, Estimated time remaining: " +
                    $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
            }
            return (cache, records);
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
    using var writer = new StreamWriter(Path.Combine(directoryName, $"ProcessedRoadTimes-Day{day}.csv"));
    writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
    foreach (var deviceRecords in processedRecords
        .GroupBy(entry => entry.DeviceIndex, (id, deviceRecords) => (ID: id, Records: deviceRecords.OrderBy(record => record.StartPingIndex)))
        .OrderBy(dev => dev.ID)
        )
    {
        foreach (var entry in deviceRecords.Records)
        {
            writer.Write(allDevices[entry.DeviceIndex][entry.StartPingIndex].DeviceID);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.StartPingIndex].Lat);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.StartPingIndex].Long);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.StartPingIndex].HAccuracy);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.StartPingIndex].TS);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.EndPingIndex].TS);
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
    ProcessRoadtimes(directory, i);
}

Console.WriteLine("Complete");

record ProcessedRecord(int DeviceIndex, int StartPingIndex, int EndPingIndex, float TravelTime, float RoadDistance, float Distance,
    HighwayType OriginRoadType, HighwayType DestinationRoadType, int NumberOfPings);

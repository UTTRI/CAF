﻿using ProcessOpenStreetMap;
using RoadNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Timers;

// Get the network file path and the root directory.
var arguments = Environment.GetCommandLineArgs();
string rootDirectory;
string networkFilePath;
if (arguments == null || arguments.Length <= 0)
{
    networkFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Panama\Panama.osmx";
    rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Panama\Day";

}
else if (arguments.Length == 2)
{
    networkFilePath = args[0];
    rootDirectory = args[1];
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
            int startingIndex = 0;
            float currentX = device[0].Lat, currentY = device[0].Long;

            void ProcessEntries(ChunkEntry startingPoint, ChunkEntry entry, int currentIndex, float straightLineDistance)
            {
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat,
                    entry.Long, cache.fastestPath, cache.dirtyBits);
                if (time < 0)
                {
                    Interlocked.Increment(ref failedPaths);
                }
                records.Add(new ProcessedRecord(deviceIndex, currentIndex, currentIndex, time, distance, straightLineDistance, originRoadType, destinationRoadType));
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
            }
            else
            {
                records.Add(new ProcessedRecord(deviceIndex, 0, 0, float.NaN, float.NaN, float.NaN, HighwayType.NotRoad, HighwayType.NotRoad));
            }
            for (int i = 1; i < device.Length; i++)
            {
                const float distanceThreshold = 0.1f;
                var straightLineDistance = Network.ComputeDistance(currentX, currentY, device[i].Lat, device[i].Long);
                // If we are in a "new location" add an entry.
                if (straightLineDistance > distanceThreshold)
                {
                    Process(startingIndex, i, straightLineDistance);
                    startingIndex = i;
                    currentX = device[i].Lat;
                    currentY = device[i].Long;
                }
                else
                {
                    // If we are not then update the current X,Y
                    var entries = (float)(i - startingIndex + 1);
                    currentX = (currentX * (entries - 1) + device[i].Lat) / entries;
                    currentY = (currentY * (entries - 1) + device[i].Long) / entries;
                    // Update where this cluster ends
                    records[^1] = records[^1] with { EndPingIndex = i };
                }
            }
            // You don't need to emit a final record if there was no travel for the final ping.
            // Store the final record to the previous day's cache
            lastEntry[device[0].DeviceID] = device[^1];
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
            writer.Write(entry.EndPingIndex - entry.StartPingIndex + 1);
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
    HighwayType OriginRoadType, HighwayType DestinationRoadType);

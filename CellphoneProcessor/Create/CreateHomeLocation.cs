using CellphoneProcessor.Utilities;
using System.Collections.Concurrent;

namespace CellphoneProcessor.Create;

internal static class CreateHomeLocation
{
    const bool ThrowOutTAZNegativeOne = true;

    public static void Run(string processedWithTAZFilePath, string shapeFilePath,
        double hourlyOffset, string outputFileLocation, string tazName, ProgressUpdate progress)
    {
        ZoneSystem zoneSystem = new(shapeFilePath, tazName);
        BlockingCollection<DeviceRecords> deviceRecords = new(Environment.ProcessorCount * 2);
        var hourOffset = (int)hourlyOffset;
        var processingTask = RunProcessingTaskAsync(processedWithTAZFilePath, deviceRecords,
            hourOffset, progress);
        ConcurrentBag<(string DeviceId, double Lat, double Lon, int Taz, int stays, int clusters)>
            completedRecords = ComputeHomeLocation(deviceRecords, zoneSystem, progress);
        // Wait for all of the results to finish reading.
        processingTask.GetAwaiter().GetResult();
        StoreRecords(completedRecords, outputFileLocation);
        progress.Current = progress.Total;
    }

    private static Task RunProcessingTaskAsync(string processedWithTAZFilePath, BlockingCollection<DeviceRecords> deviceRecords,
        int hourOffset, ProgressUpdate progress)
    {
        return Task.Run(() =>
        {
            try
            {
                string? previousDevice = null;
                List<Point> points = new();
                foreach (var line in File.ReadLines(processedWithTAZFilePath)
                                         .Skip(1))
                {
                    var parts = line.Split(',');
                    // make sure that this is a valid line
                    if (parts.Length >= 13)
                    {
                        var deviceId = parts[0];
                        if (deviceId != previousDevice)
                        {
                            // check to see if we have a real device to emit
                            if (previousDevice is not null)
                            {
                                deviceRecords.Add(new DeviceRecords(previousDevice, points));
                                points = new();
                            }
                        }
                        previousDevice = deviceId;
                        // DeviceId	Lat	Long	hAccuracy	StartTime	EndTime	TravelTime	RoadDistance	Distance	Pings	OriginRoadType	DestinationRoadType	TAZ
                        // 0        1   2       3           4           5       6           7               8           9       10              11                  12
                        int taz = int.Parse(parts[12]);
                        // Ignore stays that are outside of the zone system.
                        if (ThrowOutTAZNegativeOne && taz < 0)
                        {
                            continue;
                        }
                        double lon = double.Parse(parts[1]);
                        double lat = double.Parse(parts[2]);
                        long startTime = long.Parse(parts[4]);
                        long endTime = long.Parse(parts[5]);
                        int pings = int.Parse(parts[9]);
                        points.Add(new Point(lon, lat, pings, IsNightTime(startTime, hourOffset), endTime - startTime));
                    }
                }
                // deal with the final point
                if (previousDevice is not null && points.Count > 0)
                {
                    deviceRecords.Add(new DeviceRecords(previousDevice, points));
                }
            }
            finally
            {
                deviceRecords.CompleteAdding();
            }
        });
    }

    private static ConcurrentBag<(string DeviceId, double Lat, double Lon, int Taz, int stays, int clusters)>
    ComputeHomeLocation(BlockingCollection<DeviceRecords> deviceRecords,
        ZoneSystem zoneSystem,
        ProgressUpdate progress)
    {
        // Setup something to go through each processed device and run
        // the DBSCAN algorithm on each device, allowing each one
        // to be generated in parallel
        int numberOfDevices = 0;
        ConcurrentBag<(string DeviceId, double Lat, double Lon, int Taz, int stays, int clusters)>
            completedRecords = new();

        Parallel.ForEach(deviceRecords.GetConsumingEnumerable(), (device) =>
        {
            Interlocked.Increment(ref numberOfDevices);
            var homeLocation = DBSCAN.GetHouseholdZone(device.Points);
            completedRecords.Add((device.DeviceId, homeLocation.Lat, homeLocation.Lon,
                zoneSystem.GetTaz(homeLocation.Lat, homeLocation.Lon), device.Points.Count, homeLocation.clusters));
        });
        return completedRecords;
    }

    private static void StoreRecords(
        ConcurrentBag<(string DeviceId, double Lat, double Lon, int Taz, int stays, int clusters)> completedRecords,
        string outputFileLocation)
    {
        // Now that all of the records have finished write out the results to file
        Console.WriteLine("Writing the results out to file.");
        using var writer = new StreamWriter(outputFileLocation);
        writer.WriteLine("DeviceId,Lat,Lon,Taz,Stays,Clusters");
        foreach (var (DeviceId, Lat, Lon, Taz, Stays, Clusters) in from x in completedRecords
                                                                   orderby x.DeviceId
                                                                   select x)
        {
            writer.Write(DeviceId);
            writer.Write(',');
            writer.Write(Lat);
            writer.Write(',');
            writer.Write(Lon);
            writer.Write(',');
            writer.Write(Taz);
            writer.Write(',');
            writer.Write(Stays);
            writer.Write(',');
            writer.WriteLine(Clusters);
        }
    }

    static bool IsNightTime(long startTime, int hourOffset)
    {
        int hour = TSToHour(startTime, hourOffset);
        return hour <= 6 || hour >= 19;
    }

    static int TSToHour(long ts, int hourOffset)
    {
        var seconds = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
        var hour = (seconds.Hour + hourOffset) % 24;
        return hour >= 0 ? hour : hour + 24;
    }

    private record struct DeviceRecords(string DeviceId, List<Point> Points);
}

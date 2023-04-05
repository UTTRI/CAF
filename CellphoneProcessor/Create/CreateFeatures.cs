using Amazon.S3.Model;
using CellphoneProcessor.Utilities;

namespace CellphoneProcessor.Create;

/// <summary>
/// This class is designed to run the primary logic for generating the features
/// that go into the Mode Choice Model
/// </summary>
public static partial class CreateFeatures
{
    /// <summary>
    /// Generate the features that will go into the mode choice model.
    /// </summary>
    /// <param name="saveTo">The location to save the feature file to.</param>
    /// <param name="processedRoadtimesTAZFile">The location of a CSV of processed road times with TAZ attached.</param>
    /// <param name="transitLoSFile">The location of a CSV of processed transit times for the give </param>
    /// <param name="hourlyOffset">The offset for the given time zone.</param>
    /// <param name="updateProgress">An optional function that will take in how much of the file has been processed.</param>
    public static void GenerateModeChoiceFeatures(string saveTo, string processedRoadtimesTAZFile, string transitLoSFile,
        int hourlyOffset, Action<float>? updateProgress = null)
    {
        Time GetTime(long ts)
        {
            var time = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
            // Apply the time shift for our time zone
            var hour = (time.Hour + hourlyOffset) % 24;
            hour = hour >= 0 ? hour : hour + 24;
            // Assume that we don't have a 30 minute shift for the time zone
            return new Time() { Hour = (byte)hour, Minute = (byte)time.Minute, Second = (byte)time.Second };
        }

        IEnumerable<OutputRecord> ReadTrips(string recordsPath)
        {
            using var reader = new StreamReader(recordsPath);
            string? line = reader.ReadLine(); // burn the header
                                              // Storage for the last stay for each device
            Dictionary<string, (float Lat, float Lon, long EndTime, int taz)> lastStay = new();
            // writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
            while ((line = reader.ReadLine()) is not null)
            {
                var split = line.Split(',');
                if (split.Length >= 13)
                {
                    string device = split[0];
                    float lat = float.Parse(split[1]);
                    float lon = float.Parse(split[2]);
                    long startTime = long.Parse(split[4]);
                    long endTime = long.Parse(split[5]);
                    int taz = int.Parse(split[12]);
                    if (taz >= 0 && lastStay.TryGetValue(device, out var stay))
                    {
                        yield return new OutputRecord(device, stay.Lat, stay.Lon, lat, lon, stay.taz, taz, GetTime(stay.EndTime), GetTime(startTime));
                        updateProgress?.Invoke((float)reader.BaseStream.Position / reader.BaseStream.Length);
                    }
                    // If the record was inside of the zone system, store it
                    if (taz >= 0)
                    {
                        lastStay[device] = (lat, lon, endTime, taz);
                    }
                    else
                    {
                        // If the stay is outside of the zone system, drop it and drop the last previous stay
                        // if it exists.
                        lastStay.Remove(device);
                    }
                }
            }
        }

        using var writer = new StreamWriter(saveTo);
        writer.WriteLine("DeviceId,OriginLat,OriginLon,DestinationLat,DestinationLon,OriginTaz,DestinationTaz," +
            "Origin_Home" +
            "StartTime_MorningPeak,StartTime_MorningOffPeak,StartTime_Afternoon,StartTime_EveningPeak,StartTime_Night," +
            "Dest_Home" +
            "Endtime_MorningPeak,Endtime_MorningOffPeak,Endtime_Afternoon,Endtime_EveningPeak,Endtime_Night");

        foreach (var record in ReadTrips(processedRoadtimesTAZFile))
        {
            writer.Write(record.Device);
            writer.Write(',');
            writer.Write(record.OriginLat);
            writer.Write(',');
            writer.Write(record.OriginLon);
            writer.Write(',');
            writer.Write(record.DestinationLat);
            writer.Write(',');
            writer.Write(record.DestinationLon);
            writer.Write(',');
            writer.Write(record.OriginTaz);
            writer.Write(',');
            writer.Write(record.DestinationTaz);
            WriteTimeRecord(writer, record.TripStartTime);
            WriteTimeRecord(writer, record.TripEndTime);
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Write the 1 hot encoded data to the stream for the given time.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="time">The time to emit. </param>
    private static void WriteTimeRecord(StreamWriter writer, Time time)
    {
        var hour = time.Hour;
        writer.Write(',');
        // Morning_Peak, MoriningOffPeak,Afternoon,EveningPeak,Night
        writer.Write(IsMorningPeak(hour));
        writer.Write(',');
        writer.Write(IsMorningOffPeak(hour));
        writer.Write(',');
        writer.Write(IsAfternoon(hour));
        writer.Write(',');
        writer.Write(IsEveningPeak(hour));
        writer.Write(',');
        writer.Write(IsNight(hour));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static char IsMorningPeak(int hour) => (hour >= 6) & (hour < 9) ? '1' : '0';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static char IsMorningOffPeak(int hour) => (hour >= 9) & (hour < 12) ? '1' : '0';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static char IsAfternoon(int hour) => (hour >= 12) & (hour < 17) ? '1' : '0';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static char IsEveningPeak(int hour) => (hour >= 17) & (hour < 20) ? '1' : '0';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static char IsNight(int hour) => (hour >= 20) & (hour < 24) ? '1' : '0';

    /// <summary>
    /// A small structure to simplify writing out times.
    /// </summary>
    readonly struct Time
    {
        public byte Hour { get; init; }
        public byte Minute { get; init; }
        public byte Second { get; init; }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float operator -(Time left, Time right)
        {
            return left.AsHours() - right.AsHours();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public float AsHours()
        {
            return Hour + (Minute + (Second / 60.0f)) / 60.0f;
        }
    }

    public static async Task AppendOTPData(string serverName, string tripsFilePath, string outputPath, int threads,
        ProgressUpdate updater)
    {
        using OpenTripPlanner otp = new(EnsureStartsWithHTTP(serverName));
        var routers = await otp.GetRoutersAsync();
        otp.SetRouter(routers[0]);
        await Task.Run(() =>
        {
            var allLines = File.ReadAllLines(tripsFilePath);
            using StreamWriter writer = new(outputPath);
            // Emit the header
            writer.Write(allLines[0]);
            writer.WriteLine(",duration (min),walkTime (min),transitTime (min),waitingTime (min),transfers");
            updater.Total = allLines.Length;
            
            long processedRecords = 0;
            foreach (var entry in allLines
                    .Skip(1)
                    .AsParallel()
                    .AsOrdered()
                    .WithDegreeOfParallelism(threads)
                    .Select((row) =>
                    {
                        var parts = row.Split(',');
                        // DeviceId	OriginLat	OriginLon	DestinationLat	DestinationLon	OriginTaz	DestinationTaz	TripStartTime [ADDITIONAL ATTRIBUTES MIGHT BE HERE]
                        // 0        1           2           3               4               5           6               7             [ADDITIONAL ATTRIBUTES MIGHT BE HERE]
                        var loS = otp.GetPlanAsync(double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]), double.Parse(parts[4]), parts[7], "04-02-2019").GetAwaiter().GetResult();
                        return loS;
                    }))
            {
                processedRecords++;
                writer.Write(allLines[processedRecords]);
                writer.Write(',');
                writer.Write(entry.Walk + entry.Wait + entry.Ivtt);
                writer.Write(',');
                writer.Write(entry.Walk);
                writer.Write(',');
                writer.Write(entry.Ivtt);
                writer.Write(',');
                writer.Write(entry.Wait);
                writer.Write(',');
                writer.WriteLine(entry.Transfers);
                if (processedRecords % 1000 == 0)
                {
                    updater.Current = processedRecords;
                }
            }
            updater.Current = updater.Total;
        });
    }

    private static string EnsureStartsWithHTTP(string serverName)
    {
        if(serverName.StartsWith("https://") || serverName.StartsWith("http://"))
        {
            return serverName;
        }
        return "https://" + serverName;
    }

    /// <summary>
    /// The basic structure used for generating a record to store to file
    /// </summary>
    /// <param name="Device">The device that is being processed</param>
    /// <param name="OriginLat">The lat for the trip record's origin</param>
    /// <param name="OriginLon">The lon for the trip record's origin</param>
    /// <param name="DestinationLat">The lat for the trip's destination</param>
    /// <param name="DestinationLon">The lon for the trip's destination</param>
    /// <param name="OriginTaz">The TAZ that the trip started in</param>
    /// <param name="DestinationTaz">The TAZ that the trip ended in.</param>
    /// <param name="TripStartTime">The time that the trip started at.</param>
    /// <param name="TripEndTime">The time that the trip ended at.</param>
    record struct OutputRecord(string Device, float OriginLat, float OriginLon, float DestinationLat,
        float DestinationLon, int OriginTaz, int DestinationTaz, Time TripStartTime, Time TripEndTime);
}

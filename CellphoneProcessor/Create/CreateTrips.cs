using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace CellphoneProcessor.Create;

internal static class CreateTrips
{
    public static void Run(string staysFile, string outputFile, int hourlyOffset, ProgressUpdate progress)
    {
        progress.Total = GetFileSize(staysFile);
        using var writer = new StreamWriter(outputFile);
        writer.WriteLine("DeviceId,OriginLat,OriginLon,DestinationLat,DestinationLon,OriginTaz,DestinationTaz,TripStartTime,TripEndTime,Weekend,Weekday,TripDuration,RoadTime,RoadDistance");
        foreach (var record in ReadTrips(staysFile, hourlyOffset, progress))
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
            writer.Write(',');
            writer.Write($"{record.TripStartTime.Hour:00}:{record.TripStartTime.Minute:00}:{record.TripStartTime.Second:00}");
            writer.Write(',');
            writer.Write($"{record.TripEndTime.Hour:00}:{record.TripEndTime.Minute:00}:{record.TripEndTime.Second:00}");
            writer.Write(',');
            writer.Write(record.TripStartTime.Weekend ? '1' : '0');
            writer.Write(',');
            writer.Write(record.TripStartTime.Weekend ? '0' : '1');
            writer.Write(',');
            // Convert it to hours
            writer.Write(record.Duration / 3600.0);
            writer.Write(',');
            writer.Write(record.RoadTime);
            writer.Write(',');
            writer.WriteLine(record.RoadDistance);
        }
        // Set progress to 100% at the end
        progress.Current = progress.Total;
    }

    private static IEnumerable<Trip> ReadTrips(string recordsPath, int hourlyOffset, ProgressUpdate progress)
    {
        using var reader = new StreamReader(recordsPath);
        string? line = reader.ReadLine(); // burn the header
                                          // Storage for the last stay for each device
        Dictionary<string, (float Lat, float Lon, long EndTime, int taz)> lastStay = new();
        long recordsProcessed = 0;
        // writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
        while ((line = reader.ReadLine()) is not null)
        {
            var split = line.Split(',');
            if (split.Length < 13)
            {
                continue;
            }
            string device = split[0];
            float lat = float.Parse(split[1]);
            float lon = float.Parse(split[2]);
            long startTime = long.Parse(split[4]);
            long endTime = long.Parse(split[5]);
            int taz = int.Parse(split[12]);
            if (taz >= 0 && lastStay.TryGetValue(device, out var stay))
            {
                float roadTime = float.Parse(split[6]);
                float roadDistance = float.Parse(split[7]);
                yield return new Trip(device, stay.Lat, stay.Lon, lat, lon, stay.taz, taz, GetTime(stay.EndTime, hourlyOffset), GetTime(startTime, hourlyOffset), startTime - stay.EndTime, roadTime, roadDistance);
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
            // If we have processed 1k trips update the progress
            recordsProcessed++;
            if(recordsProcessed % 1000 == 0)
            {
                progress.Current = reader.BaseStream.Position;
            }
        }
    }

    private static Time GetTime(long ts, int hourlyOffset)
    {
        var time = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
        // Apply the time shift for our time zone
        var hour = (time.Hour + hourlyOffset) % 24;
        hour = hour >= 0 ? hour : hour + 24;
        // Assume that we don't have a 30 minute shift for the time zone
        return new Time() { Hour = (byte)hour, Minute = (byte)time.Minute, Second = (byte)time.Second, Weekend = IsWeekend(time) };
    }

    private static bool IsWeekend(DateTime time)
    {
        return time.DayOfWeek switch
        {
            DayOfWeek.Sunday => true,
            DayOfWeek.Saturday => true,
            _ => false
        };
    }

    private static double GetFileSize(string staysFile)
    {
        var info = new FileInfo(staysFile);
        return info.Length;
    }

    private struct Time
    {
        public byte Hour;
        public byte Minute;
        public byte Second;
        public bool Weekend;
    }

    record struct Trip(string Device, float OriginLat, float OriginLon, float DestinationLat,
    float DestinationLon, int OriginTaz, int DestinationTaz, Time TripStartTime, Time TripEndTime, long Duration,
    float RoadTime, float RoadDistance);
}

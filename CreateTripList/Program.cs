

/*
Create trip list for cellphone data
    Lat Lon Origin
    Lat Lon Destination
    Start Time of Trip in ["hh:mm:ss"]
    record per trip 
 */

string stayRecordFile = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\ProcessedRoadTimes-WithTAZ.csv";
string outputFile = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\Trips.csv";
var hourlyOffset = -3; // BuenosAires
//var hourlyOffset = -5; // Bogota + Panama

Time GetTime(long ts)
{
    var time = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
    // Apply the time shift for our time zone
    var hour = (time.Hour + hourlyOffset) % 24;
    hour = hour >= 0 ? hour : hour + 24;
    // Assume that we don't have a 30 minute shift for the time zone
    return new Time() { Hour = (byte)hour, Minute = (byte)time.Minute, Second = (byte)time.Second, Weekend = IsWeekend(time) };
}

bool IsWeekend(DateTime time)
{
    return time.DayOfWeek switch
    {
        DayOfWeek.Sunday => true,
        DayOfWeek.Saturday => true,
        _ => false
    };
}

IEnumerable<Trip> ReadTrips(string recordsPath)
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
                float roadTime = float.Parse(split[6]);
                float roadDistance = float.Parse(split[7]);
                yield return new Trip(device, stay.Lat, stay.Lon, lat, lon, stay.taz, taz, GetTime(stay.EndTime), GetTime(startTime), startTime - stay.EndTime, roadTime, roadDistance);
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

using var writer = new StreamWriter(outputFile);
writer.WriteLine("DeviceId,OriginLat,OriginLon,DestinationLat,DestinationLon,OriginTaz,DestinationTaz,TripStartTime,TripEndTime,TripDuration,Weekend,Weekday,RoadTime,RoadDistance");

foreach (var record in ReadTrips(stayRecordFile))
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

struct Time
{
    public byte Hour;
    public byte Minute;
    public byte Second;
    public bool Weekend;
}

record struct Trip(string Device, float OriginLat, float OriginLon, float DestinationLat,
    float DestinationLon, int OriginTaz, int DestinationTaz, Time TripStartTime, Time TripEndTime, long Duration,
    float RoadTime, float RoadDistance);

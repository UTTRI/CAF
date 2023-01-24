
using System.Collections;
using System.Linq;

string recordsPath = args.Length > 0 ? args[0] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes-WithTAZ.csv";
string outputPath = args.Length > 0 ? args[1] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\Demand";

//var hourlyOffset = -3; // BuenosAires
var hourlyOffset = -5; // Bogota + Panama

// Initialize the demand records
var demandRecords = new Dictionary<(int Origin, int Destination), long[]>[2];
for (int i = 0; i < 24; i++)
{
    demandRecords[0] = new();
    demandRecords[1] = new();
}

// Given a device id the last time and zone the device was recorded at.
Dictionary<string, ((int EndTime, int Weekday) Time, int Destination)> previousZone = new();

// 0 for Weekday, 1 for Weekend
static int GetWeekdayIndex(DayOfWeek dayOfWeek)
{
    return dayOfWeek switch
    {
        DayOfWeek.Saturday => 1,
        DayOfWeek.Sunday => 1,
        _ => 0
    };
}

// Get the hour from the ts converted into local time
(int Weekday, int Hour) TSToHour(long ts)
{
    var seconds = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
    var hour = (seconds.Hour + hourlyOffset) % 24;
    hour = hour >= 0 ? hour : hour + 24;
    return (GetWeekdayIndex(seconds.DayOfWeek), hour);
}


// Get the OD and Hour for this record
(int Origin, int Destination, int Hour, int Weekday) ProcessRecord(string line)
{
    // writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
    var parts = line.Split(',');
    if (parts.Length <= 12)
    {
        return (-1, -1, -1, -1);
    }
    var deviceId = parts[0];
    var destinationTAZ = int.Parse(parts[12]);

    // Check to see if we are going to be going outside of the zone system
    if (destinationTAZ == -1)
    {
        previousZone.Remove(deviceId);
        return (-1, -1, -1, -1);
    }
    var endHour = TSToHour(long.Parse(parts[5]));
    
    var originTAZ = -1;
    var tripStart = (Hour: -1, Weekday: -1);
    if (previousZone.TryGetValue(deviceId, out var previous))
    {
        originTAZ = previous.Destination;
        tripStart = previous.Time;
    }
    previousZone[deviceId] = ((endHour.Hour, endHour.Weekday), destinationTAZ);
    return (originTAZ, destinationTAZ, tripStart.Hour, tripStart.Weekday);
}

// Collect the demand from the records

using var reader = new StreamReader(recordsPath);
string? line = reader.ReadLine(); // burn the header
while ((line = reader.ReadLine()) is not null)
{
    var entry = ProcessRecord(line);
    if ((entry.Origin >= 0)
        & (entry.Destination >= 0)
        & (entry.Hour >= 0))
    {

        static void AddDemand(Dictionary<(int Origin, int Destination), long[]> storage, int origin, int destination, int hour)
        {
            if (!storage.TryGetValue((origin, destination), out var records))
            {
                storage[(origin, destination)] = records = new long[24];
            }
            records[hour]++;
        }
        AddDemand(demandRecords[entry.Weekday], entry.Origin, entry.Destination, entry.Hour);
    }
}

// Output all of the demand matrices

static void WriteMatrices(string fileName, Dictionary<(int Origin, int Destination), long[]> demand)
{
    using var writer = new StreamWriter(fileName);
    writer.Write("Origin,Destination");
    for (int i = 0; i < 24; i++)
    {
        writer.Write(",Hour");
        writer.Write(i);
    }
    writer.WriteLine(",Total");
    foreach (var entry in demand.OrderBy(entry => (entry.Key.Origin, entry.Key.Destination)))
    {
        writer.Write(entry.Key.Origin);
        writer.Write(',');
        writer.Write(entry.Key.Destination);
        for (int i = 0; i < entry.Value.Length; i++)
        {
            writer.Write(',');
            writer.Write(entry.Value[i]);
        }
        writer.Write(',');
        writer.WriteLine(entry.Value.Sum());
    }
}

static void WriteOrigins(string fileName, Dictionary<(int Origin, int Destination), long[]> demand)
{
    using var writer = new StreamWriter(fileName);
    writer.Write("Origin");
    for (int i = 0; i < 24; i++)
    {
        writer.Write(",Hour");
        writer.Write(i);
    }
    writer.WriteLine(",Total");
    foreach (var entry in demand
            .GroupBy(entry => entry.Key.Origin)
            .OrderBy(entry => entry.Key))
    {
        writer.Write(entry.Key);
        for (int i = 0; i < 24; i++)
        {
            writer.Write(',');
            writer.Write(entry.Sum(x => x.Value[i]));
        }
        writer.Write(',');
        writer.WriteLine(entry.Sum(x => x.Value.Sum()));
    }
}

static void WriteDestinations(string fileName, Dictionary<(int Origin, int Destination), long[]> demand)
{
    using var writer = new StreamWriter(fileName);
    writer.Write("Destination");
    for (int i = 0; i < 24; i++)
    {
        writer.Write(",Hour");
        writer.Write(i);
    }
    writer.WriteLine(",Total");
    foreach (var entry in demand
            .GroupBy(entry => entry.Key.Destination)
            .OrderBy(entry => entry.Key))
    {
        writer.Write(entry.Key);
        for (int i = 0; i < 24; i++)
        {
            writer.Write(',');
            writer.Write(entry.Sum(x => x.Value[i]));
        }
        writer.Write(',');
        writer.WriteLine(entry.Sum(x => x.Value.Sum()));
    }
}

// Write out all of the matrices in parallel
Parallel.Invoke(
    () => WriteOrigins(EnsureExists(outputPath, "WeekdayOrigins.csv"), demandRecords[0]),
    () => WriteDestinations(EnsureExists(outputPath, "WeekdayDestinations.csv"), demandRecords[0]),
    () => WriteMatrices(EnsureExists(outputPath, "WeekdayMatrix.csv"), demandRecords[0]),
    () => WriteOrigins(EnsureExists(outputPath, "WeekendOrigins.csv"), demandRecords[1]),
    () => WriteDestinations(EnsureExists(outputPath, "WeekendDestinations.csv"), demandRecords[1]),
    () => WriteMatrices(EnsureExists(outputPath, "WeekendMatrix.csv"), demandRecords[1])
);


static string EnsureExists(string outputDir, string fileName)
{
    var info = new DirectoryInfo(outputDir);
    if (!info.Exists)
    {
        info.Create();
    }
    return Path.Combine(outputDir, fileName);
}


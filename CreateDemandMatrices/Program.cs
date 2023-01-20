
string recordsPath = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\ProcessedRoadTimes-WithTAZ.csv";
string outputPath = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\Demand";
var hourlyOffset = -5;

// Initialize the demand records
var demandRecords = new Dictionary<(int Origin, int Destination), long>[24];
for (int i = 0; i < 24; i++)
{
    demandRecords[i] = new();
}

// Given a device id the last time and zone the device was recorded at.
Dictionary<string, (int EndTime, int Destination)> previousZone = new();

// Get the hour from the ts converted into local time
int TSToHour(long ts)
{
    var hour = (TimeSpan.FromSeconds(ts).Hours + hourlyOffset) % 24;
    return hour >= 0 ? hour : hour + 24;
}

// Get the OD and Hour for this record
(int Origin, int Destination, int Hour) ProcessRecord(string line)
{
    // writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
    var parts = line.Split(',');
    if (parts.Length <= 12)
    {
        return (-1, -1, -1);
    }
    var deviceId = parts[0];
    var endHour = TSToHour(long.Parse(parts[5]));
    var destinationTAZ = int.Parse(parts[12]);
    var originTAZ = -1;
    var tripStartHour = -1;
    if (previousZone.TryGetValue(deviceId, out var previous))
    {
        originTAZ = previous.Destination;
        tripStartHour = previous.EndTime;
    }
    previousZone[deviceId] = (endHour, destinationTAZ);
    return (originTAZ, destinationTAZ, tripStartHour);
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
        if (!demandRecords[entry.Hour].TryGetValue((entry.Origin, entry.Destination), out long records))
        {
            records = 0;
        }
        demandRecords[entry.Hour][(entry.Origin, entry.Destination)] = records + 1;
    }
}

// Output all of the demand matrices

static void WriteMatrix(string fileName, Dictionary<(int Origin, int Destination), long> demand)
{
    using var writer = new StreamWriter(fileName);
    writer.WriteLine("Origin,Destination,Records");
    foreach (var entry in demand.OrderBy(entry => (entry.Key.Origin, entry.Key.Destination)))
    {
        writer.Write(entry.Key.Origin);
        writer.Write(',');
        writer.Write(entry.Key.Destination);
        writer.Write(',');
        writer.WriteLine(entry.Value);
    }
}

// Write out all of the matrices in parallel
Parallel.For(0, demandRecords.Length, (int i) =>
{
    WriteMatrix(EnsureExists(outputPath, $"DemandHour-{i}.csv"), demandRecords[i]);
});

static string EnsureExists(string outputDir, string fileName)
{
    var info = new DirectoryInfo(outputDir);
    if (!info.Exists)
    {
        info.Create();
    }
    return Path.Combine(outputDir, fileName);
}


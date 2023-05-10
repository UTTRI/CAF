string recordsPath = args.Length > 0 ? args[0] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes-WithTAZ.csv";
string outputPath = args.Length > 0 ? args[1] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\Distances";
var hourlyOffset = args.Length > 0 ? int.Parse(args[2]) : -3;

//var hourlyOffset = -3; // BuenosAires
//var hourlyOffset = -5; // Bogota + Panama


// Weekday = 0, Weekend = 1, 200 500m bins.
var distanceBins = new long[][]
{
    new long[200],
    new long[200],
};

var roadTimeBins = new long[][]
{
    new long[288],
    new long[288],
};

var clusterGapTimeBins = new long[][]
{
    new long[288],
    new long[288],
};

Dictionary<string, PreviousEntry> previousEntry = new();

// Process the records
foreach (var record in File.ReadLines(recordsPath)
    .Skip(1))
{
    ProcessRecord(record);
}

// Store the results
WriteVector(EnsureExists(outputPath, "Weekdays-Distance.csv"), "DistanceKM", 0.5, distanceBins[0]);
WriteVector(EnsureExists(outputPath, "Weekends-Distance.csv"), "DistanceKM", 0.5, distanceBins[1]);

WriteVector(EnsureExists(outputPath, "Weekdays-RoadTime.csv"), "RoadTimeMin", 5, roadTimeBins[0]);
WriteVector(EnsureExists(outputPath, "Weekends-RoadTime.csv"), "RoadTimeMin", 5, roadTimeBins[1]);

WriteVector(EnsureExists(outputPath, "Weekdays-ClusterGapTime.csv"), "TripTimeMin", 5, clusterGapTimeBins[0]);
WriteVector(EnsureExists(outputPath, "Weekends-ClusterGapTime.csv"), "TripTimeMin", 5, clusterGapTimeBins[1]);

static void WriteVector(string filePath, string columnName, double indexFactor, long[] bins)
{
    using var writer = new StreamWriter(filePath);
    writer.Write(columnName);
    writer.WriteLine(",Records");

    for (int i = 0; i < bins.Length; i++)
    {
        writer.Write(i * indexFactor);
        writer.Write(',');
        writer.WriteLine(bins[i]);
    }
}

void ProcessRecord(string line)
{
    //"DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ"
    var parts = line.Split(',');
    var deviceId = parts[0];
    var destinationTAZ = int.Parse(parts[12]);
    // Check to see if we are going to be going outside of the zone system
    if (destinationTAZ == -1)
    {
        previousEntry.Remove(deviceId);
        return;
    }
    var endTimeTS = long.Parse(parts[5]);
    (var weekday, var endTime) = TSToHour(endTimeTS);
    if (previousEntry.TryGetValue(deviceId, out var previous))
    {
        var startTimeTS = long.Parse(parts[4]);
        (_, var startTime) = TSToHour(startTimeTS);
        distanceBins[previous.Weekday][GetDistanceBin(double.Parse(parts[8]))]++;
        roadTimeBins[previous.Weekday][GetTimeBin(double.Parse(parts[6]))]++;
        clusterGapTimeBins[previous.Weekday][GetTimeBin((endTimeTS - startTimeTS) /60.0)]++;

        // Update the previous
        previous.Weekday = weekday;
        previous.EndTime = endTime;
        previous.EndTimeTS = endTimeTS;
    }
    else
    {
        previous = new PreviousEntry(weekday, endTime, endTimeTS);
        previousEntry[deviceId] = previous;
    }
}


static int GetDistanceBin(double distance)
{
    return Math.Min(Math.Max(0, (int)Math.Floor(distance / 2.0)), 199);
}

static int GetTimeBin(double time)
{
    return Math.Min(Math.Max(0, (int)Math.Floor(time / 5.0)), 287);
}


static string EnsureExists(string outputDir, string fileName)
{
    var info = new DirectoryInfo(outputDir);
    if (!info.Exists)
    {
        info.Create();
    }
    return Path.Combine(outputDir, fileName);
}

// Get the hour from the ts converted into local time
(int Weekday, int Hour) TSToHour(long ts)
{
    var seconds = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
    var hour = (seconds.Hour + hourlyOffset) % 24;
    hour = hour >= 0 ? hour : hour + 24;
    return (GetWeekdayIndex(seconds.DayOfWeek), hour);
}

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

internal class PreviousEntry
{
    internal int Weekday;
    internal long EndTime;
    internal long EndTimeTS;

    public PreviousEntry(int weekday, long endTime, long endTimeTS)
    {
        Weekday = weekday;
        EndTime = endTime;
        EndTimeTS = endTimeTS;
    }
}


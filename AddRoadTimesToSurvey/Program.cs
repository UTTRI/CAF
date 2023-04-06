using RoadNetwork;

var networkFilePath = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\BuenosAires.osmx";
var surveyFilePath = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\SurveyTrips.csv";
var outputFilePath = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\SurveyTravelTimes.csv";

Console.WriteLine("Loading Road Network...");
Network network = new(networkFilePath);

// hhld_id,pers_id,trip_id, start_time,origin_zone,origin_zone_x,origin_zone_y,dest_zone,dest_zone_x,dest_zone_y
// 0       1       2        3          4           5             6             7         8           9


var records = File.ReadLines(surveyFilePath)
    .Skip(1)
    .Select(line => line.Split(','))
    .Select(parts => (OX : float.Parse(parts[6]), OY:float.Parse(parts[5]),
                      DX : float.Parse(parts[9]), DY: float.Parse(parts[8])))
    .ToArray();

float[] travelTimeResults = new float[records.Length];
float[] travelDistanceResults = new float[records.Length];

int processedRecords = 0;

Parallel.For(0, records.Length,
    () => network.GetCache(),
    (i, _, networkCache) =>
    {
        // Get origin
        var results = network.Compute(records[i].OX, records[i].OY, records[i].DX, records[i].DY, networkCache.fastestPath, networkCache.dirtyBits);
        travelTimeResults[i] = results.time;
        travelDistanceResults[i] = results.distance;
        var processed = Interlocked.Increment(ref processedRecords);
        if (processed % 10000 == 0)
        {
            Console.Write($"\rProcessed Record {processed} of {records.Length}");
        }
        return networkCache;
    },
    (cache) => { } // Do nothing
);

Console.WriteLine();

using var writer = new StreamWriter(outputFilePath);
writer.WriteLine("RoadTime,RoadDistance");
for (int i = 0;i < travelTimeResults.Length;i++)
{
    writer.Write(travelTimeResults[i]);
    writer.Write(',');
    writer.WriteLine(travelDistanceResults[i]);
}

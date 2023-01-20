using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

static (Geometry[], int[] tazNumber) ReadShapeFile(string path, string tazFieldName)
{
    var accGeo = new List<Geometry>();
    var accTaz = new List<int>();
    var reader = new ShapefileDataReader(path, NetTopologySuite.Geometries.GeometryFactory.Default);

    // Get the TAZ field index
    int tazFieldIndex = reader.GetOrdinal(tazFieldName);
    if (tazFieldIndex < 0)
    {
        throw new Exception($"The shape file did not have any column {tazFieldName} for the TAZ");
    }
    while (reader.Read())
    {
        var geo = reader.Geometry;

        if (geo is Geometry p)
        {
            accGeo.Add(p);
            accTaz.Add(reader.GetInt32(tazFieldIndex));
        }
    }
    return (accGeo.ToArray(), accTaz.ToArray());
}

static int IndexOfCollision(float x, float y, Geometry[] zoneSystem)
{
    // For some reason the map has lat and long inverted
    var point = GeometryFactory.Default.CreatePoint(new Coordinate(y, x));
    for (int i = 0; i < zoneSystem.Length; i++)
    {
        if (zoneSystem[i].Contains(point))
        {
            return i;
        }
    }
    return -1;
}

var shapeFilePath = args.Length != 0 ? args[0] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Shapefile\bogata_zone_epsg4326.shp";
var tazName = args.Length != 0 ? args[1] : "ZAT";
var recordsPath = args.Length != 0 ? args[2] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes.csv";
var outputPath = args.Length != 0 ? args[3] : @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes-WithTAZ.csv";
(var zoneSystem, var taz) = ReadShapeFile(shapeFilePath, tazName);


Console.WriteLine(zoneSystem.Length);

(string original, int zoneNumber) ProcessRecord(string line)
{
    var parts = line.Split(',');
    var x = float.Parse(parts[1]);
    var y = float.Parse(parts[2]);
    var index = IndexOfCollision(x, y, zoneSystem);
    var zoneNumber = index >= 0 ? taz[index] : -1;
    return (line, zoneNumber);
}

// Load in each record
using var writer = new StreamWriter(outputPath);
//writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
foreach (var processedLine in File.ReadLines(recordsPath)
    .Skip(1)
    .AsParallel()
    .AsOrdered()
    .Select(line => ProcessRecord(line)))
{
    writer.Write(processedLine.original);
    writer.Write(',');
    writer.WriteLine(processedLine.zoneNumber);
}

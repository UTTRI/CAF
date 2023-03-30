using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace CellphoneProcessor.Utilities;

internal sealed class ZoneSystem
{
    private readonly IndexedPointInAreaLocator[] _zoneSystemLocator;
    private readonly int[] _tazNumber;

    public ZoneSystem(string shapeFile, string tazFieldName)
    {
        (var zoneSystem, _tazNumber) = ReadShapeFile(shapeFile, tazFieldName);
        _zoneSystemLocator = zoneSystem.Select(z => new IndexedPointInAreaLocator(z)).ToArray();

    }

    private static (Polygon[], int[] tazNumber) ReadShapeFile(string path, string tazFieldName)
    {
        var accGeo = new List<Polygon>();
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

            if (geo is Polygon p)
            {
                accGeo.Add(p);
                accTaz.Add(reader.GetInt32(tazFieldIndex));
            }
        }
        return (accGeo.ToArray(), accTaz.ToArray());
    }

    public int GetTaz(double lat, double lon)
    {
        var index = IndexOfCollision(lat, lon, _zoneSystemLocator);
        return index >= 0 ? _tazNumber[index] : -1; ;
    }

    private int IndexOfCollision(double lat, double lon, IndexedPointInAreaLocator[] zoneSystem)
    {
        // For some reason the map has lat and long inverted
        var point = new Coordinate(lat, lon);
        for (int i = 0; i < zoneSystem.Length; i++)
        {
            var test = _zoneSystemLocator?[i].Locate(point) ?? Location.Null;
            if (!test.HasFlag(Location.Exterior))
            {
                return i;
            }
        }
        return -1;
    }
}

using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;

namespace CellphoneProcessor.Utilities;

internal sealed class ZoneSystem
{
    private readonly IndexedPointInAreaLocator[] _zoneSystemLocator;
    private readonly int[] _tazNumber;

    public ZoneSystem(string shapeFile, string tazFieldName)
    {
        (var zoneSystem, _tazNumber) = Utilities.ShapefileHelper.ReadShapeFile(shapeFile, tazFieldName);
        _zoneSystemLocator = zoneSystem.Select(z => new IndexedPointInAreaLocator(z)).ToArray();

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

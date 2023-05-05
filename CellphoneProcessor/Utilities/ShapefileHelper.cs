using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace CellphoneProcessor.Utilities;

/// <summary>
/// Provides functions for working with ESRI Shape files
/// </summary>
internal static class ShapefileHelper
{
    /// <summary>
    /// Gets the names of the attributes found in the shape file
    /// </summary>
    /// <param name="fileName">The file path to the shape file</param>
    /// <returns>A list of columns contained in the shape file, an empty list if there was an issue.</returns>
    internal static List<string> GetColumns(string fileName)
    {
        List<string>? columns = null;
        try
        {
            var factory = NetTopologySuite.Geometries.GeometryFactory.Default;
            var reader = new ShapefileDataReader(fileName, factory);
            columns = reader.DbaseHeader.Fields
                .Select(field => field.Name)
                .ToList();
        }
        catch
        {
            // We catch-all here because if we can't get the columns
            // the correct answer is to just return that there were none
        }
        return columns ?? new List<string>();
    }

    /// <summary>
    /// Read in the shape file and associate the geometry with the given TAZ
    /// </summary>
    /// <param name="path">The path to the shape file</param>
    /// <param name="tazFieldName">The name of the TAZ column</param>
    /// <returns></returns>
    /// <exception cref="Exception">Thrown if the column does not exist</exception>
    public static (Polygon[], int[] tazNumber) ReadShapeFile(string path, string tazFieldName)
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
}

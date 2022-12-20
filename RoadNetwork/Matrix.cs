namespace RoadNetwork;

/// <summary>
/// Stores a matrix of demand for the network
/// </summary>
public class Matrix
{
    /// <summary>
    /// The backing memory for the matrix
    /// </summary>
    private readonly float[] _data;

    /// <summary>
    /// The number of columns in each row.
    /// </summary>
    private readonly int _rowSize;

    /// <summary>
    /// Create a new matrix with the given number of rows
    /// </summary>
    /// <param name="numberOfRows"></param>
    public Matrix(int numberOfRows)
    {
        _rowSize = numberOfRows;
        _data = new float[numberOfRows * numberOfRows];
    }

    /// <summary>
    /// Load a matrix from the given file name.
    /// </summary>
    /// <param name="csvFileName">The file path to the CSV File</param>
    /// <returns>A matrix with the values from the given file.</returns>
    public static Matrix LoadMatrixFromCSV(string csvFileName, ZoneSystem zoneSystem)
    {
        var records = File.ReadLines(csvFileName)
                        .Skip(1) // skip the header
                        .AsParallel()
                        // no need for this to be in order
                        .Select(l => l.Split(','))
                        .Where(l => l.Length >= 3)
                        .Select(parts => (Origin: int.Parse(parts[0]), Destination: int.Parse(parts[1]), Value: float.Parse(parts[2])))
                        .ToArray();
        var ret = new Matrix(zoneSystem.Length);
        var data = ret.Data;
        for (int i = 0; i < records.Length; i++)
        {
            var origin = zoneSystem.GetIndexForZoneNumber(records[i].Origin);
            var destination = zoneSystem.GetIndexForZoneNumber(records[i].Destination);
            if (origin == -1 || destination == -1)
            {
                throw new Exception("Zone not found.");
            }
            data[ret.RowLength * origin + destination] = records[i].Value;
        }
        return ret;
    }

    /// <summary>
    /// Get the backing data for the matrix
    /// </summary>
    public float[] Data { get => _data; }

    /// <summary>
    /// Returns the number of zones in the matrix
    /// </summary>
    public int RowLength { get => _rowSize; }

    /// <summary>
    /// The total number of cells in the matrix
    /// </summary>
    public int Length { get => _data.Length; }

    /// <summary>
    /// Gets the starting index in the data for the given row.
    /// </summary>
    /// <param name="row">The row to get the index of.</param>
    /// <returns>The index into the data array where the row starts.</returns>
    public int IndexOfRow(int row) => _rowSize * row;
}

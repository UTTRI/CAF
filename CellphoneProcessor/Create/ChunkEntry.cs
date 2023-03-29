using System.IO;

namespace CellphoneProcessor.Create;

internal record struct ChunkEntry
    (string DeviceID, float Lat, float Long, long TS, float HAccuracy)
{
    internal static ChunkEntry[][] LoadOrderedChunks(string directoryName)
    {
        if (!Directory.Exists(directoryName))
        {
            throw new Exception($"Directory '{directoryName}' does not exist!");
        }
        DirectoryInfo dir = new(directoryName);
        var chunkFiles = dir.GetFiles("Chunk*.csv");
        var chunkAcc = new Dictionary<string, List<ChunkEntry>>[chunkFiles.Length];
        // Load all of the chunks in parallel
        Parallel.For(0, chunkFiles.Length, (int i) =>
        {
            chunkAcc[i] = new();
            foreach (var parts in File.ReadLines(chunkFiles[i].FullName)
                    .Skip(1)
                    .Select(l => l.Split(',')))
            {
                // "did,lat,lon,ts,haccuracy"
                if (parts.Length < 5)
                {
                    continue;
                }
                var entry = new ChunkEntry(parts[0],
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    long.Parse(parts[3]),
                    float.Parse(parts[4]));
                if (!chunkAcc[i].TryGetValue(entry.DeviceID, out var list))
                {
                    list = new List<ChunkEntry>();
                    chunkAcc[i][entry.DeviceID] = list;
                }
                list.Add(entry);
            }
        });
        // Combine the chunks together
        var acc = chunkAcc[0];
        for (int i = 1; i < chunkAcc.Length; i++)
        {
            foreach (var entry in chunkAcc[i])
            {
                if (!acc.TryGetValue(entry.Key, out var list))
                {
                    acc[entry.Key] = entry.Value;
                }
                else
                {
                    list.AddRange(entry.Value);
                }
            }
        }
        // Sort the results by device name and order the chunks
        return acc
            .Keys
            .OrderBy(k => k)
            .Select(k => acc[k].OrderBy(c => c.TS).ToArray())
            .ToArray();
    }
}


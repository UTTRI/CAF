namespace ProcessOpenStreetMap;

internal record struct ChunkEntry
    (string DeviceID, float Lat, float Long, float TS, float HAccuracy)
{
    public static IEnumerable<ChunkEntry> EnumerateEntries(string filePath)
    {
        foreach (var parts in File.ReadLines(filePath)
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
                float.Parse(parts[3]), 
                float.Parse(parts[4]));
            yield return entry;
        }
    }
}


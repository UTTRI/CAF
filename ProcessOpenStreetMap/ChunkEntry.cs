namespace ProcessOpenStreetMap;

internal record struct ChunkEntry
    (string DeviceID, float Lat, float Long, float TS, float HAccuracy)
{
    public static IEnumerable<ChunkEntry> EnumerateEntries(string path)
    {
        if(Directory.Exists(path))
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            foreach(var file in dir.EnumerateFiles("*.csv"))
            {
                foreach (var parts in File.ReadLines(file.FullName)
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
        else
        {
            foreach (var parts in File.ReadLines(path)
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
}


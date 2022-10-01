namespace GatherAWSData;

internal sealed record class Entry
    (string Lat, string Long, string TS, string HAccuracy)
{
    // Original Structure: 'did','idtype','lat','lon','ts','haccuracy','device_brand','device_model','telco','ip','country','src','h10'

    // Target Structure: 'did','lat','lon','ts','haccuracy'

    public static Dictionary<string, List<Entry>> LoadEntries(string filePath)
    {
        var ret = new Dictionary<string, List<Entry>>();
        foreach (var parts in File.ReadLines(filePath).Skip(1).Select(l => l.Split(',')))
        {
            if (parts.Length < 13)
            {
                continue;
            }
            var id = parts[0];
            var entry = new Entry(parts[2], parts[3], parts[4], parts[5]);
            if (!ret.TryGetValue(id, out var list))
            {
                ret[id] = list = new List<Entry>();
            }
            list.Add(entry);
        }
        return ret;
    }
}

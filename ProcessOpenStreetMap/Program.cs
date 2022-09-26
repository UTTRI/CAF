#define PARALLEL

using ProcessOpenStreetMap;
using System.Diagnostics;
using System.Threading;

Network network = new(@"./Rio.osmx");
Console.WriteLine("Finished loading network.");
var allDevices = ChunkEntry.EnumerateEntries(@"./Chunked-2019.09.02")
    .GroupBy(chunk => chunk.DeviceID, (_, chunkGroup) => chunkGroup.OrderBy(c2 => c2.TS).ToArray())
    .ToArray();
Console.WriteLine("Finished loading Entries...");
int processedDevices = 0;
int failedPaths = 0;
Console.WriteLine("Starting to process entries.");
var watch = Stopwatch.StartNew();
#if PARALLEL
Parallel.ForEach(allDevices,
    ()=>
    {
        return network.GetCache();
    },
    (device, _, cache) =>
#else
foreach (var device in allDevices)
#endif
{
    for (int i = 1; i < device.Length; i++)
    {
        var startingPoint = device[i - 1];
        var entry = device[i];
        var (time, distance) = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat, entry.Long, cache.fastestPath, cache.dirtyBits);
        if (time < 0)
        {
            Interlocked.Increment(ref failedPaths);
        }
    }
    var p = Interlocked.Increment(ref processedDevices);
    if (p % 100 == 0)
    {
        var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allDevices.Length - p));
        Console.Write($"Processing {p} of {allDevices.Length}, Estimated time remaining: " +
            $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
    }
    return cache;
}
#if PARALLEL
, (cache) => { }
);
#endif
watch.Stop();
Console.WriteLine($"\n{failedPaths} were unable to be computed.");
Console.WriteLine($"Total runtime for entries: {watch.ElapsedMilliseconds}ms");

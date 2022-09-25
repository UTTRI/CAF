#define PARALLEL

using ProcessOpenStreetMap;
using System.Diagnostics;
using System.Threading;

Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Rio.osmx");
Console.WriteLine("Finished loading network.");
var startingPoint = ChunkEntry.EnumerateEntries(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Chunked-2019.09.02\Chunk-1.csv").First();
var allEntries = ChunkEntry.EnumerateEntries(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Chunked-2019.09.02\Chunk-1.csv").ToArray();
Console.WriteLine("Finished loading Entries...");
int processed = 0;
int failedPaths = 0;
Console.WriteLine("Starting to process entries.");
var watch = Stopwatch.StartNew();
#if PARALLEL
Parallel.ForEach(allEntries, entry =>
#else
foreach (var entry in allEntries)
#endif
{
    var (time, distance) = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat, entry.Long);
    var p = Interlocked.Increment(ref processed);
    if(time < 0)
    {
        Interlocked.Increment(ref failedPaths);
    }
    if (p % 100 == 0)
    {
        var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allEntries.Length - p));
        Console.Write($"Processing {processed} of {allEntries.Length}, Estimated time remaining: " +
            $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
    }
}
#if PARALLEL
);
#endif
watch.Stop();
Console.WriteLine($"\n{failedPaths} were unable to be computed.");
Console.WriteLine($"Total runtime for entries: {watch.ElapsedMilliseconds}ms");

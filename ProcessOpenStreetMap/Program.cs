// See https://aka.ms/new-console-template for more information

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
//foreach(var entry in allEntries)
Parallel.ForEach(allEntries, entry =>
{
    var results = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat, entry.Long);
    Interlocked.Increment(ref processed);
    if(results.time < 0)
    {
        Interlocked.Increment(ref failedPaths);
    }
    if (processed % 100 == 0)
    {
        var ts = TimeSpan.FromMilliseconds((watch.ElapsedMilliseconds / processed) * (allEntries.Length - processed));
        Console.Write($"Processing {processed} of {allEntries.Length}, Estimated time remaining: " +
            $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
    }
});
Console.WriteLine($"\n{failedPaths} were unable to be computed.");
Console.WriteLine();

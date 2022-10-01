using GatherAWSData;
using System.IO.Compression;

var awsConfig = new AWSConfig("awsConfig.txt");

var areaName = "greater_rio_de_janeiro_metropolitan_area_br";
var year = 2019;
var month = 9;

for(int day = 1; day <= 30; day++)
{
    Console.WriteLine($"Starting to process day {day}");
    var directory = $"DownloadedFiles/Day{day}";
    Console.WriteLine("Downloading files...");
    var fileNames = await AWS.DownloadData(awsConfig, directory, areaName, year, month, day);
    Console.WriteLine("Decompressing files");
    // Decompress the files and update the fileNames
    Parallel.For(0, fileNames.Length, (int i) =>
    {
        if (fileNames[i] is string fileName)
        {
            var csvName = GetCSVFileName(fileName);
            DecompressFile(fileName, csvName);
            // Clean up the disk space
            File.Delete(fileName);
            fileNames[i] = csvName;
        }
    });
    Console.WriteLine("Chunking files...");
    ChunkFiles(fileNames);
}

static void DecompressFile(string filePathSrc, string filePathDest)
{
    using var fileStream = new FileStream(filePathSrc, FileMode.Open, FileAccess.Read);
    using var compressionStream = new GZipStream(fileStream, CompressionMode.Decompress);
    using var writer = new FileStream(filePathDest, FileMode.Create, FileAccess.Write);
    compressionStream.CopyTo(writer);
}

static string GetCSVFileName(string compressedFileName)
{
    return Path.Combine(Path.GetDirectoryName(compressedFileName) ?? "", Path.GetFileNameWithoutExtension(compressedFileName) + ".csv");
}

static void ChunkFiles(string?[] csvNames)
{
    var fileEntries = new Dictionary<string, List<Entry>>[csvNames.Length];    
    Parallel.For(0, fileEntries.Length, (int i) =>
    {
        if (csvNames[i] is string fileName)
        {
            fileEntries[i] = Entry.LoadEntries(fileName);
        }
        else
        {
            fileEntries[i] = new Dictionary<string, List<Entry>>();
        }
    });

    Console.WriteLine("Combining entries...");
    var combineIn = fileEntries[0];
    for (int i = 1; i < fileEntries.Length; i++)
    {
        foreach (var otherEntry in fileEntries[i])
        {
            if (!combineIn.TryGetValue(otherEntry.Key, out var device))
            {
                combineIn[otherEntry.Key] = otherEntry.Value;
            }
            else
            {
                // If it already exists, combine them
                foreach (var deviceEntry in otherEntry.Value)
                {
                    device.Add(deviceEntry);
                }
                // try to reclaim some memory
                otherEntry.Value.Clear();
            }
        }
    }


    Console.WriteLine("Balancing Chunks");
    const int numberOfChunks = 10;
    var keys = combineIn.Keys.ToArray();
    var size = keys.Select(key => combineIn[key].Count).ToArray();
    var totalSize = size.Sum();
    var chunks = new (int start, int stop)[numberOfChunks];

    void FindChunkIndexes()
    {
        int currentChunk = 0;
        int start = 0;
        int i = 0;
        var acc = 0;
        var chunkSize = totalSize / numberOfChunks;
        for (; i < keys.Length && currentChunk < numberOfChunks - 1; i++)
        {
            acc += size[i];
            if (acc >= chunkSize)
            {
                chunks[currentChunk] = (start, i);
                currentChunk++;
                start = i;
                acc = 0;
            }
        }
        chunks[currentChunk] = (start, keys.Length);
    }
    FindChunkIndexes();

    Parallel.For(0, numberOfChunks, (int chunkIndex) =>
    {
        var directory = Path.GetDirectoryName(csvNames[0])!;
        var startIndex = chunks[chunkIndex].start;
        // if it is the last index
        var endIndex = chunks[chunkIndex].stop;
        using var writer = new StreamWriter(Path.Combine(directory, $"Chunk-{chunkIndex}.csv"));
        writer.WriteLine("did,lat,lon,ts,haccuracy");
        // Target Structure: 'did','lat','lon','ts','haccuracy'
        for (int i = startIndex; i < endIndex; i++)
        {
            var list = combineIn[keys[i]];
            foreach (var entry in list)
            {
                writer.Write(keys[i]);
                writer.Write(',');
                writer.Write(entry.Lat);
                writer.Write(',');
                writer.Write(entry.Long);
                writer.Write(',');
                writer.Write(entry.TS);
                writer.Write(',');
                writer.WriteLine(entry.HAccuracy);
            }
        }
    });
}
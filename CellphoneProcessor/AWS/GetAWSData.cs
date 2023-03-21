using Amazon;
using Amazon.S3;
using Amazon.S3.Model;


namespace CellphoneProcessor.AWS;

public static class GetAWSData
{
    private static async Task<string?[]> DownloadData(AWSConfig config, string destinationDirectory, string prefix, string areaName, int year, int month, int date)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }
        string?[] fileNames = Array.Empty<string>();
        using (var client = new AmazonS3Client(config.AwsKey, config.AwsSecret, Amazon.RegionEndpoint.USWest2))
        {
            var awsNames = await CreateAWSDataSourceNames(config, client, prefix, areaName, year, month, date);
            fileNames = CreateFileNames(destinationDirectory, awsNames);
            var cancel = new CancellationToken();
            for (int i = 0; i < awsNames.Length; i++)
            {
                try
                {
                    var response = await client.GetObjectAsync(config.BucketName, awsNames[i]);
                    await response.WriteResponseStreamToFileAsync(fileNames[i], false, cancel);
                }
                catch
                {
                    fileNames[i] = null;
                }
            }
        }
        return fileNames;
    }

    private static async Task<string[]> CreateAWSDataSourceNames(AWSConfig config, AmazonS3Client client, string prefix,
        string areaName, int year, int month, int date)
    {
        var p = String.IsNullOrEmpty(areaName)
            ? $"{prefix}/{year}/{month:00}/{date:00}"
            : $"{prefix}/{areaName}/{year}/{month:00}/{date:00}";
        var request = new ListObjectsRequest()
        {
            BucketName = config.BucketName,
            Prefix = p
        };
        var namesRequest = await client.ListObjectsAsync(request);
        return namesRequest.S3Objects.Where(x => !x.Key.Equals(prefix)).Select(x => x.Key).ToArray();
    }

    private static string[] CreateFileNames(string directory, string[] awsNames)
    {
        return awsNames.Select(x => Path.Combine(directory, x.Split('/').Last())).ToArray();
    }

    internal static async Task MainAsync(AWSConfig config, string downloadTo, string areaName, string prefix, int year, int month,
        Action<int> updateCurrentDateDownloaded, Action<int> updateCurrentDateChunked)
    {
        for (int day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            var directory = $"{downloadTo}/Day{day}";
            var fileNames = await DownloadData(config, directory, prefix, areaName, year, month, day);
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
            updateCurrentDateDownloaded(day);
            ChunkFiles(fileNames);
            updateCurrentDateChunked(day);
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

        void ChunkFiles(string?[] csvNames)
        {
            var fileEntries = new Dictionary<string, List<Entry>>[csvNames.Length];
            Parallel.For(0, fileEntries.Length, (int i) =>
            {
                fileEntries[i] = csvNames[i] is string fileName
                                    ? Entry.LoadEntries(fileName)
                                    : new Dictionary<string, List<Entry>>();
            });

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
    }
}

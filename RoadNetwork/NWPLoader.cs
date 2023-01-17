using System.IO.Compression;

namespace RoadNetwork;

internal static class NWPLoader
{
    internal static List<Node> LoadNWPNetwork(string path)
    {
        
        using var archive = ZipFile.OpenRead(path);
        var nodes = LoadNodes(archive.GetEntry("base.211") ?? throw new InvalidDataException());
        return nodes;
    }

    private static List<Node> LoadNodes(ZipArchiveEntry zipArchiveEntry)
    {
        var ret = new List<Node>();
        using var reader = new StreamReader(zipArchiveEntry.Open());
        string? line;
        // start with nodes
        bool nodes = true;
        while((line = reader.ReadLine()) is not null)
        {
            if (line.Length < 2)
            {
                continue;
            }
            switch(line[0])
            {
                case 'c':
                    continue;
                case 't':
                    nodes = (line == "t nodes");
                    continue;
                case 'a':
                    break;
            }
            
            var splits = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length > 0)
            {

            }
        }
        return ret;
    }
}

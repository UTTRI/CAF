using System.Runtime.InteropServices;

namespace CellphoneProcessor.Compare;

internal static class CompareTrips
{
    internal static Task Run(string ourTripFile, List<string> theirTripFiles, string outputFile)
    {
        return Task.Run(() =>
        {
            Dictionary<string, int> ourTrips = null!;
            Dictionary<string, int> theirTrips = null!;
            Parallel.Invoke(
                    // Create a mapping for the number of activities that we have
                    () => ourTrips = ComputeOurTrips(ourTripFile),
                    // Create a mapping for the number of activities that they have
                    () => theirTrips = ComputeTheirTrips(theirTripFiles)
            );
            OutputComparison(ourTrips, theirTrips, outputFile);
        });
    }

    private static Dictionary<string, int> ComputeOurTrips(string ourTripFile)
    {
        // DeviceId OriginLat OriginLon DestinationLat DestinationLon TripStartTime
        // 0        1         2         3              4              5
        var ret = new Dictionary<string, int>();
        using var reader = new StreamReader(ourTripFile);
        string? line = reader.ReadLine(); // burn the header
        while ((line = reader.ReadLine()) is not null)
        {
            string devId = line[..line.IndexOf(',')];
            // WARNING: The following line is high performance but requires that there is nothing
            // that could be writing to this dictionary at the same time as when we are using the result.
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(ret, devId, out _);
            // and increase our count of this
            count++;
        }
        return ret;
    }

    private static Dictionary<string, int> ComputeTheirTrips(List<string> theirTripFiles)
    {
        var ret = new Dictionary<string, int>();
        // device_id origin_geoid destination_geoid origin_lat origin_long destination_lat destination_long start_timestamp end_timestamp hour_of_day time_period day_of_week trip_duration trip_distance trip_speed year iso_week iso_week_start_date iso_week_end_date
        // 0         1            2                 3          4           5               6                7               8             9           10          11          12            13            14         15   16       17                  18
        for (int i = 0; i < theirTripFiles.Count; i++)
        {
            using var reader = new StreamReader(theirTripFiles[i]);
            string? line = reader.ReadLine(); // burn the header
            while ((line = reader.ReadLine()) is not null)
            {
                string devId = line[..line.IndexOf(',')];
                // WARNING: The following line is high performance but requires that there is nothing
                // that could be writing to this dictionary at the same time as when we are using the result.
                ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(ret, devId, out _);
                // and increase our count of this
                count++;
            }
        }
        return ret;
    }

    /// <summary>
    /// Write out the comparison
    /// </summary>
    /// <param name="ourTrips">Our trips stored as device id mapped to the number of trips</param>
    /// <param name="theirTrips">Their trips stored as device id mapped to the number of trips</param>
    /// <param name="outputFile">The name of the file to write to</param>
    private static void OutputComparison(Dictionary<string, int> ourTrips, Dictionary<string, int> theirTrips, string outputFile)
    {
        using var writer = new StreamWriter(outputFile);
        writer.WriteLine("DeviceID,OurTripCount,TheirTripCount");
        void WriteEntry(string deviceId, int ourValue, int theirValue)
        {
            writer.Write(deviceId);
            writer.Write(',');
            writer.Write(ourValue);
            writer.Write(',');
            writer.WriteLine(theirValue);
        }
        // Go through each device that we have and then lookup their activities and write the correspondence
        foreach (var entry in ourTrips)
        {
            if (!theirTrips.TryGetValue(entry.Key, out var theirValue))
            {
                theirValue = 0;
            }
            WriteEntry(entry.Key, entry.Value, theirValue);
        }
        // Go through each device that they have and write out the zeros for devices that we have no record of
        foreach (var entry in theirTrips)
        {
            if (!ourTrips.TryGetValue(entry.Key, out _))
            {
                WriteEntry(entry.Key, 0, entry.Value);
            }
        }
    }
}

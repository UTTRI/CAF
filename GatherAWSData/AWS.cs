using Amazon.S3;
using System.Threading.Channels;

namespace GatherAWSData;

static internal class AWS
{
    public static async Task<string?[]> DownloadData(AWSConfig config, string destinationDirectory, string areaName, int year, int month, int date)
    {
        if(!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }
        var awsNames = CreateAWSDataSourceNames(areaName, year, month, date);
        string?[] fileNames = CreateFileNames(destinationDirectory, areaName, year, month, date);
        var cancel = new CancellationToken();
        using (var client = new AmazonS3Client(config.AwsKey, config.AwsSecret, Amazon.RegionEndpoint.USWest2))
        {
            for(int i = 0; i < awsNames.Length; i++)
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

    private static string[] CreateAWSDataSourceNames(string areaName, int year, int month, int date)
    {
        return new string[]
        {
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_00000.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_00001.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_00002.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_00003.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_00004.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_10000.gz",
            $"norm_data/{areaName}/{year}/{month:00}/{date:00}/lotadata_{areaName}_{year}_{month:00}_{date:00}_10001.gz",
        };
    }

    private static string[] CreateFileNames(string directory, string areaName, int year, int month, int date)
    {
        return new string[]
        {

            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_00000.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_00001.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_00002.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_00003.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_00004.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_10000.gz"),
            Path.Combine(directory, $"lotadata_{areaName}_{year}_{month:00}_{date:00}_10001.gz"),
        };
    }
}

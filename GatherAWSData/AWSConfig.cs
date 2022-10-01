namespace GatherAWSData;

internal sealed class AWSConfig
{
    public readonly string AwsKey;
    public readonly string AwsSecret;
    public readonly string BucketName;

    public AWSConfig(string fileName)
    {
        var lines = File.ReadAllLines(fileName);
        if(lines is null || lines.Length < 3)
        {
            throw new Exception("Invalid AWS Configuration");
        }
        AwsKey = lines[0];
        AwsSecret = lines[1];
        BucketName = lines[2];
    }
}

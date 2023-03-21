namespace CellphoneProcessor.AWS;

/// <summary>
/// Provides information required to access Amazon S3
/// </summary>
/// <param name="AwsKey">Key</param>
/// <param name="AwsSecret">Secret</param>
/// <param name="BucketName">Bucket</param>
internal sealed record AWSConfig(string AwsKey, string AwsSecret, string BucketName);

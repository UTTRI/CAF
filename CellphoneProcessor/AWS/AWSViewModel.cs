using System.Runtime.CompilerServices;

namespace CellphoneProcessor.AWS;

/// <summary>
/// Provides the base logic for Downloading and Chunking data
/// from AWS.
/// </summary>
public sealed class AWSViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// The key for AWS
    /// </summary>
    public string AWSKey
    {
        get => _awsKey;
        set
        {
            _awsKey = value;
            UpdateAndValidateParameters();
        }
    }

    private void UpdateAndValidateParameters([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        CanRun =
            !String.IsNullOrEmpty(AWSSecret)
            && !String.IsNullOrEmpty(AWSKey)
            && !String.IsNullOrEmpty(BucketName)
            && !String.IsNullOrEmpty(DownloadFolder);
    }

    /// <summary>
    /// The AWS Secret to use when logging in.
    /// </summary>
    public string AWSSecret
    {
        get => _awsSecret;
        set
        {
            _awsSecret = value;
            UpdateAndValidateParameters();
        }
    }

    /// <summary>
    /// The name of the bucket to get the data from.
    /// </summary>
    public string BucketName
    {
        get => _bucketName;
        set
        {
            _bucketName = value;
            UpdateAndValidateParameters();
        }
    }

    public string DownloadFolder
    {
        get => _downloadFolder;
        set
        {
            _downloadFolder = value;
            UpdateAndValidateParameters();
        }
    }

    public DateTime SurveyDate
    {
        get => _surveyDate;
        set
        {
            _surveyDate = value;
            UpdateAndValidateParameters();
            UpdateTotalDays();
        }
    }

    public int TotalDays
    {
        get => _totalDays;
        private set
        {
            _totalDays = value;
            UpdateAndValidateParameters();
        }
    }

    public int DaysDownloaded
    {
        get => _daysDownloaded;
        set
        {
            _daysDownloaded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DaysDownloaded)));
        }
    }

    public int DaysChunked
    {
        get => _daysChunked;
        set
        {
            _daysChunked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DaysChunked)));
        }
    }

    private void UpdateTotalDays()
    {
        TotalDays = DateTime.DaysInMonth(SurveyDate.Year, SurveyDate.Month);
    }

    public AWSViewModel()
    {
        var parameters = Configuration.Shared?.GetParameters(nameof(AWSViewModel)) ?? new();
        Configuration.TryUpdateValue(parameters, nameof(AWSKey), ref _awsKey);
        Configuration.TryUpdateValue(parameters, nameof(AWSSecret), ref _awsSecret);
        Configuration.TryUpdateValue(parameters, nameof(BucketName), ref _bucketName);
        UpdateTotalDays();
        UpdateAndValidateParameters();
    }

    /// <summary>
    /// Set to true when all of the data is valid
    /// </summary>
    public bool CanRun
    {
        get => _canRun;
        private set
        {
            _canRun = value;
            // We don't need to update the validation because that is updating this.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRun)));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void Save()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(AWSViewModel));
        parameters[nameof(AWSKey)] = AWSKey;
        parameters[nameof(AWSSecret)] = AWSSecret;
        parameters[nameof(BucketName)] = BucketName;
    }

    public async Task StartProcessingAsync()
    {

        // var areaName = "greater_rio_de_janeiro_metropolitan_area_br";
        // var prefix = "norm_data";
        // var areaName = "greater_bogota_metropolitan_area_co";
        // var prefix = "norm_data/co";
        var areaName = "";
        var prefix = "norm_data/pa";
        // var areaName = "greater_buenos_aires_ar";
        // var prefix = "norm_data/ar";
        // var year = 2019;
        // var month = 9;

        await GetAWSData.MainAsync(
            new AWSConfig(_awsKey, _awsSecret, _bucketName),
            DownloadFolder,
            areaName,
            prefix,
            SurveyDate.Year,
            SurveyDate.Month,
            (currentDay) => DaysDownloaded = currentDay,
            (currentDay) => DaysChunked = currentDay);
    }

    #region Backing Variables
    private string _awsKey = string.Empty;
    private string _awsSecret = string.Empty;
    private string _bucketName = string.Empty;
    private string _downloadFolder = string.Empty;
    private int _totalDays = 0;
    private DateTime _surveyDate = new(2019, 9, 1);
    private bool _canRun = false;
    private int _daysDownloaded = 0;
    private int _daysChunked;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

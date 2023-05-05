namespace CellphoneProcessor.Create;

/// <summary>
/// This class is used to automate the CreateTripsPage
/// </summary>
internal sealed class CreateTripsViewModel : INotifyPropertyChanged
{
    public string StaysFilePath
    {
        get => _staysFilePath;
        set
        {
            _staysFilePath = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StaysFilePath)));
            UpdateCanRun();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputPath)));
            UpdateCanRun();
        }
    }

    public bool CanRun
    {
        get => _canRun;
        private set
        {
            _canRun = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRun)));
        }
    }

    public bool PageEnabled
    {
        get => _pageEnabled;
        private set
        {
            _pageEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

    public double HourlyOffset
    {
        get => _hourlyOffset;
        set
        {
            _hourlyOffset = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyOffset)));
        }
    }

    public string HourlyTextOffset
    {
        get => _hourlyOffset.ToString();
        set
        {
            double.TryParse(value, out _hourlyOffset);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyOffset)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyTextOffset)));
        }
    }

    public bool IsRunning
    {
        get => !_pageEnabled;
    }

    private void UpdateCanRun()
    {
        CanRun = !(String.IsNullOrWhiteSpace(StaysFilePath)
            || String.IsNullOrWhiteSpace(OutputPath));
    }

    public double ProgressCurrent
    {
        get => _progressCurrent;
        set
        {
            _progressCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressCurrent)));
        }
    }

    public double ProgressTotal
    {
        get => _progressTotal;
        set
        {
            _progressTotal = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressTotal)));
        }
    }

    public CreateTripsViewModel()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateTripsViewModel));
        string hourly = "0";
        Configuration.TryUpdateValue(parameters, nameof(StaysFilePath), ref _staysFilePath);
        Configuration.TryUpdateValue(parameters, nameof(OutputPath), ref _outputPath);
        Configuration.TryUpdateValue(parameters, nameof(HourlyOffset), ref hourly);
        _ = double.TryParse(hourly, out _hourlyOffset);
        UpdateCanRun();
    }

    private void UpdateConfiguration()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateTripsViewModel));
        parameters[nameof(StaysFilePath)] = StaysFilePath;
        parameters[nameof(OutputPath)] = OutputPath;
        parameters[nameof(HourlyOffset)] = HourlyOffset.ToString();
        Configuration.Shared.Save();
    }

    public async Task CreateTripsAsync()
    {
        PageEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                ProgressUpdate updater = new();
                updater.PropertyChanged += (_, e) =>
                {
                    switch(e.PropertyName)
                    {
                        case nameof(updater.Current):
                            ProgressCurrent = updater.Current;
                            break;
                        case nameof(updater.Total):
                            ProgressTotal = updater.Total;
                            break;
                    }
                };
                UpdateConfiguration();
                CreateTrips.Run(StaysFilePath, OutputPath, (int)HourlyOffset, updater);
            });
        }
        finally
        {
            PageEnabled = true;
        }
    }

    #region BackingVariables
    private string _staysFilePath = string.Empty;
    private bool _canRun = false;
    private string _outputPath = string.Empty;
    private bool _pageEnabled = true;
    private double _progressCurrent = 0.0;
    private double _progressTotal = 0.0;
    private double _hourlyOffset;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

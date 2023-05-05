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

    public int HourlyOffset
    {
        get => _hourlyOffset;
        set
        {
            _hourlyOffset = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyOffset)));
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
                CreateTrips.Run(StaysFilePath, OutputPath, HourlyOffset, updater);
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
    private int _hourlyOffset;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

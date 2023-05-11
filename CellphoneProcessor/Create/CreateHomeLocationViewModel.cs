using CellphoneProcessor.Utilities;

namespace CellphoneProcessor.Create;

internal class CreateHomeLocationViewModel : INotifyPropertyChanged
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

    public string ShapeFilePath
    {
        get => _shapeFilePath;
        set
        {
            _shapeFilePath = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShapeFilePath)));
            LoadShapeFileColumns();
            UpdateCanRun();
        }
    }

    public string TAZName
    {
        get => _tazName;
        set
        {
            _tazName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TAZName)));
            UpdateCanRun();
        }
    }

    public List<string> ShapeFileColumns
    {
        get => _shapeFileColumns;
        private set
        {
            _shapeFileColumns = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShapeFileColumns)));
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
        set
        {
            _canRun = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRun)));
        }
    }

    private void UpdateCanRun()
    {
        CanRun = !(String.IsNullOrEmpty(StaysFilePath)
            || String.IsNullOrEmpty(OutputPath)
            || String.IsNullOrWhiteSpace(ShapeFilePath)
            || String.IsNullOrWhiteSpace(TAZName));
    }

    private void LoadShapeFileColumns()
    {
        var newColumns = ShapefileHelper.GetColumns(ShapeFilePath);
        if(newColumns is not null)
        {
            ShapeFileColumns = newColumns;
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

    public bool PageEnabled
    {
        get => _pageEnabled;
        set
        {
            _pageEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageEnabled)));
        }
    }

    public async Task RunAsync()
    {
        PageEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                SaveConfiguration();
                ProgressUpdate update = new();
                update.PropertyChanged += (_, e) =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(update.Current):
                            ProgressCurrent = update.Current;
                            break;
                        case nameof(update.Total):
                            ProgressTotal = update.Total;
                            break;
                    }
                };
                CreateHomeLocation.Run(StaysFilePath, ShapeFilePath, HourlyOffset,
                    OutputPath, TAZName, update);
            });
        }
        finally
        {
            PageEnabled = true;
        }
    }

    public CreateHomeLocationViewModel()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateHomeLocationViewModel));
        // TODO: Implement loading the parameters
        string hourlyOffsetText = string.Empty;
        Configuration.TryUpdateValue(parameters, nameof(StaysFilePath), ref _staysFilePath);
        Configuration.TryUpdateValue(parameters, nameof(ShapeFilePath), ref _shapeFilePath);
        Configuration.TryUpdateValue(parameters, nameof(TAZName), ref _tazName);
        Configuration.TryUpdateValue(parameters, nameof(OutputPath), ref _outputPath);
        Configuration.TryUpdateValue(parameters, nameof(HourlyOffset), ref hourlyOffsetText);
        HourlyTextOffset = hourlyOffsetText;
        LoadShapeFileColumns();
        UpdateCanRun();
    }

    internal void SaveConfiguration()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateHomeLocationViewModel));
        parameters[nameof(StaysFilePath)] = _staysFilePath;
        parameters[nameof(ShapeFilePath)] = _shapeFilePath;
        parameters[nameof(TAZName)] = _tazName;
        parameters[nameof(OutputPath)] = _outputPath;
        parameters[nameof(HourlyOffset)] = _hourlyOffset.ToString();
    }

    #region BackingVariables
    private string _staysFilePath = string.Empty;
    private string _outputPath = string.Empty;
    private string _shapeFilePath = string.Empty;
    private bool _canRun = false;
    private double _hourlyOffset = -3;
    private double _progressCurrent = 0;
    private double _progressTotal = 1;
    private bool _pageEnabled = true;
    private string _tazName = String.Empty;
    private List<string> _shapeFileColumns = new List<string>();

    public event PropertyChangedEventHandler? PropertyChanged;
    #endregion
}

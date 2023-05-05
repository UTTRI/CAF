using NetTopologySuite.Algorithm;

namespace CellphoneProcessor.Create;

/// <summary>
/// The view model for CreateStaysPage
/// </summary>
public sealed class CreateStaysViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gives the path to the chunk folder
    /// </summary>
    public string ChunkFolder
    {
        get => _chunkFolder;
        set
        {
            _chunkFolder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChunkFolder)));
            UpdateIfReadyToRun();
        }
    }

    /// <summary>
    /// The location of the shape file to use for identifying TAZ
    /// </summary>
    public string ShapeFile
    {
        get => _shapeFile;
        set
        {
            _shapeFile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShapeFile)));
            UpdateShapefileColumns();
            UpdateIfReadyToRun();
        }
    }

    /// <summary>
    /// The column within the shape file to use for identifying TAZ
    /// </summary>
    public string TAZColumn
    {
        get => _tazColumn;
        set
        {
            _tazColumn = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TAZColumn)));
            UpdateIfReadyToRun();
        }
    }

    /// <summary>
    /// This will be set to true when the inputs are valid
    /// </summary>
    public bool ReadyToRun
    {
        get => _readyToRun;
        set
        {
            _readyToRun = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReadyToRun)));
        }
    }

    /// <summary>
    /// This contains the list of all of the columns in the shape file
    /// </summary>
    public List<string> ShapeFileColumns
    {
        get => _shapeFileColumns;
        private set
        {
            _shapeFileColumns = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShapeFileColumns)));
        }
    }

    /// <summary>
    /// This contains the total amount of folders that will be processed.
    /// </summary>
    public int FoldersToProcess
    {
        get => _foldersToProcess;
        private set
        {
            _foldersToProcess = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FoldersToProcess)));
        }
    }

    /// <summary>
    /// The number of devices that are contained in the clustering
    /// </summary>
    public int NumberOfDevices
    {
        get => _numberOfDevices;
        private set
        {
            _numberOfDevices = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberOfDevices)));
        }
    }

    /// <summary>
    /// This contains the index of the folder that is currently being processed.
    /// </summary>
    public int CurrentFolder
    {
        get => _currentFolder;
        private set
        {
            _currentFolder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFolder)));
        }
    }

    /// <summary>
    /// This contains the index of the device that is currently being processed.
    /// </summary>
    public int CurrentTAZ
    {
        get => _currentTAZ;
        private set
        {
            _currentTAZ = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTAZ)));
        }
    }

    /// <summary>
    /// This contains the index of the device that is currently being processed.
    /// </summary>
    public int CurrentRoad
    {
        get => _currentRoad;
        private set
        {
            _currentRoad = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentRoad)));
        }
    }

    /// <summary>
    /// This contains the index of the device that is currently being processed.
    /// </summary>
    public int CurrentWriting
    {
        get => _currentWriting;
        private set
        {
            _currentWriting = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentWriting)));
        }
    }

    /// <summary>
    /// This contains the file path to where we will save the resulting stays.
    /// </summary>
    public string OutputFile
    {
        get => _outputFile;
        set
        {
            _outputFile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputFile)));
            UpdateIfReadyToRun();
        }
    }

    /// <summary>
    /// This points to the location where the file that contains the road network is located.
    /// </summary>
    public string RoadNetwork
    {
        get => _roadNetwork;
        set
        {
            _roadNetwork = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RoadNetwork)));
            UpdateIfReadyToRun();
        }
    }

    /// <summary>
    /// This variable will be set to true if we are not currently executing.
    /// </summary>
    public bool PageEnabled
    {
        get => _pageEnabled;
        set
        {
            _pageEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageEnabled)));
        }
    }

    /// <summary>
    /// This variable is used to make sure that only the last request to update the columns
    /// actually updates the list.
    /// </summary>
    private long _updateShapefileTicket = 0;

    private void UpdateShapefileColumns()
    {
        // Check to make sure that there is actually a shape file to lookup
        if (String.IsNullOrWhiteSpace(_shapeFile))
        {
            return;
        }
        var ticket = Interlocked.Increment(ref _updateShapefileTicket);
        Task.Run(() =>
        {
            var ret = Utilities.ShapefileHelper.GetColumns(_shapeFile);
            // Only update if this was the last shape file opened
            // This is the check, lock, check strategy to reduce conflicts
            if (Interlocked.Read(ref _updateShapefileTicket) == ticket)
            {
                lock (this)
                {
                    if (Interlocked.Read(ref _updateShapefileTicket) == ticket)
                    {
                        ShapeFileColumns = ret;
                    }
                }
            }
        });
    }

    private void UpdateIfReadyToRun()
    {
        ReadyToRun = !(String.IsNullOrWhiteSpace(TAZColumn)
                    || String.IsNullOrWhiteSpace(ShapeFile)
                    || String.IsNullOrWhiteSpace(OutputFile)
                    || String.IsNullOrWhiteSpace(ChunkFolder)
                    || String.IsNullOrWhiteSpace(RoadNetwork)
                    );
    }

    private bool _isRunning = false;

    internal async Task StartRun()
    {
        if (_isRunning)
        {
            return;
        }
        _isRunning = true;
        PageEnabled = false;
        UpdateConfig();
        try
        {
            ProgressUpdate chunkingProgress = new();
            chunkingProgress.PropertyChanged += (_, e) =>
            {
                switch(e.PropertyName)
                {
                    case nameof(chunkingProgress.Total):
                        FoldersToProcess = (int)chunkingProgress.Total;
                        break;
                    case nameof(chunkingProgress.Current): 
                        CurrentFolder = (int)chunkingProgress.Current;
                        break;
                }
            };
            ProgressUpdate tazProgress = new();
            tazProgress.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(tazProgress.Total):
                        NumberOfDevices = (int)tazProgress.Total;
                        break;
                    case nameof(tazProgress.Current):
                        CurrentTAZ = (int)tazProgress.Current;
                        break;
                }
            };
            ProgressUpdate roadProgress = new();
            roadProgress.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(roadProgress.Total):
                        NumberOfDevices = (int)roadProgress.Total;
                        break;
                    case nameof(roadProgress.Current):
                        CurrentRoad = (int)roadProgress.Current;
                        break;
                }
            };
            ProgressUpdate writingProgress = new();
            writingProgress.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(writingProgress.Total):
                        NumberOfDevices = (int)writingProgress.Total;
                        break;
                    case nameof(writingProgress.Current):
                        CurrentWriting = (int)writingProgress.Current;
                        break;
                }
            };
            await CreateStays.RunAsync(ChunkFolder, ShapeFile, TAZColumn, RoadNetwork, OutputFile,
                chunkingProgress, tazProgress, roadProgress, writingProgress);
        }
        finally
        {
            _isRunning = false;
            PageEnabled = true;
        }
    }

    private void UpdateConfig()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateStaysViewModel));
        parameters[nameof(ShapeFile)] = ShapeFile;
        parameters[nameof(ChunkFolder)] = ChunkFolder;
        parameters[nameof(TAZColumn)] = TAZColumn;
        parameters[nameof(RoadNetwork)] = RoadNetwork;
        Configuration.Shared.Save();
    }

    /// <summary>
    /// Create a view for CreateStaysPage
    /// </summary>
    public CreateStaysViewModel()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateStaysViewModel));
        Configuration.TryUpdateValue(parameters, nameof(ShapeFile), ref _shapeFile);
        Configuration.TryUpdateValue(parameters, nameof(ChunkFolder), ref _chunkFolder);
        Configuration.TryUpdateValue(parameters, nameof(TAZColumn), ref _tazColumn);
        Configuration.TryUpdateValue(parameters, nameof(RoadNetwork), ref _roadNetwork);
        UpdateShapefileColumns();
        UpdateIfReadyToRun();
    }

    #region Backing Variables
    private string _chunkFolder = String.Empty;
    private string _shapeFile = String.Empty;
    private string _tazColumn = "TAZ";
    private bool _readyToRun = false;
    private List<string> _shapeFileColumns = new();
    private string _outputFile = String.Empty;
    private int _foldersToProcess = 0;
    private bool _pageEnabled = true;
    private string _roadNetwork = String.Empty;
    private int _numberOfDevices = 0;
    private int _currentFolder = 0;
    private int _currentRoad = 0;
    private int _currentTAZ = 0;
    private int _currentWriting = 0;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

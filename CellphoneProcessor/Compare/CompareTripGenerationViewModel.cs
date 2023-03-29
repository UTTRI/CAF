namespace CellphoneProcessor.Compare;

public sealed class CompareTripGenerationViewModel : INotifyPropertyChanged
{
    public string OurTripFile
    {
        get => _ourTripFile;
        set
        {
            _ourTripFile = value;
            UpdatePropertyChanged();
        }
    }

    public ObservableCollection<string> CompareAgainstTripFiles
    {
        get => _compareAgainstTripFiles;
        set
        {
            _compareAgainstTripFiles = value;
            UpdatePropertyChanged();
        }
    }

    public string OutputFile
    {
        get => _outputFile;
        set
        {
            _outputFile = value;
            UpdatePropertyChanged();
        }
    }

    private void UpdatePropertyChanged([CallerMemberName] string? functionName = null)
    {
        if (functionName is not null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(functionName));
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCompare)));
    }

    public bool CanCompare
    {
        get => !String.IsNullOrWhiteSpace(_ourTripFile)
            && _compareAgainstTripFiles.Count > 0
            && !String.IsNullOrEmpty(_outputFile);
    }

    public Task CompareFiles()
    {
        return CompareTrips.Run(_ourTripFile, _compareAgainstTripFiles.ToList(), _outputFile);
    }

    internal void AddTheirFile(string name)
    {
        CompareAgainstTripFiles.Add(name);
    }

    internal bool RemoveTheirFile(string name)
    {
        return CompareAgainstTripFiles.Remove(name);
    }

    #region Backing Variables
    private string _ourTripFile = String.Empty;
    private ObservableCollection<string> _compareAgainstTripFiles = new();
    private string _outputFile = String.Empty;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

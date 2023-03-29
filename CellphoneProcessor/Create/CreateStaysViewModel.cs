namespace CellphoneProcessor.Create;

public sealed class CreateStaysViewModel : INotifyPropertyChanged
{
    public string ChunkFolder
    {
        get => _chunkFolder;
        set
        {
            _chunkFolder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChunkFolder)));
        }
    }

    public string ShapeFile
    {
        get => _shapeFile;
        set
        {
            _shapeFile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShapeFile)));
        }
    }

    public CreateStaysViewModel()
    {
        var parameters = Configuration.Shared.GetParameters(nameof(CreateStaysViewModel));
        Configuration.TryUpdateValue(parameters, nameof(ShapeFile), ref _shapeFile);
        Configuration.TryUpdateValue(parameters, nameof(ChunkFolder), ref _chunkFolder);

    }

    #region Backing Variables
    private string _chunkFolder = String.Empty;
    private string _shapeFile = String.Empty;
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
}

namespace CellphoneProcessor.Create;

public class ProgressUpdate : INotifyPropertyChanged
{
    private double _current;
    private double _total;
    public double Current
    {
        get => _current;
        set
        {
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        }
    }

    public double Total
    {
        get => _total;
        set
        {
            _total = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

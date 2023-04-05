using CellphoneProcessor.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;
using System.Configuration;
using System.Windows.Threading;

namespace CellphoneProcessor.Create;

public sealed class CreateFeaturesViewModel : INotifyPropertyChanged
{
    private double _processedRecords;
    private double _numberOfRecords;
    private DateTime _startTime = DateTime.Now;
    private string _serverName = "http://emme-windows.vaughan.local:8080";
    public string _otpRouterName = "Current";
    private string _tripFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\Trips.csv";
    private string _outputFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\Transit.csv";
    private bool _isRunning = false;
    private int _otpServerThreads = System.Environment.ProcessorCount * 2;

    public CreateFeaturesViewModel()
    {

    }

    public bool CanRun
    {
        get => ValidateInputs();
    }

    private bool ValidateInputs()
    {
        return
            !String.IsNullOrEmpty(OTPServerName)
            && !String.IsNullOrEmpty(TripFilePath)
            && !String.IsNullOrEmpty(OutputFilePath)
            && !String.IsNullOrEmpty(OTPRouterName)
            && !IsRunning
            && _otpServerThreads > 0;
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            ValidateInputs();
        }
    }

    public string OTPServerName
    {
        get => _serverName;
        set
        {
            _serverName = value;
            InputsUpdated();
        }
    }

    private void InputsUpdated([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
        {
            return;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRun)));
    }

    public string OTPRouterName
    {
        get => _otpRouterName;
        set
        {
            _otpRouterName = value;
            InputsUpdated();
        }
    }

    public int OTPServerThreads
    {
        get => _otpServerThreads;
        set
        {
            _otpServerThreads = value;
            InputsUpdated();
        }
    }

    public string TripFilePath
    {
        get => _tripFilePath;
        set
        {
            _tripFilePath = value;
            InputsUpdated();
        }
    }

    public string OutputFilePath
    {
        get => _outputFilePath;
        set
        {
            _outputFilePath = value;
            InputsUpdated();
        }
    }

    public double ProcessedRecords
    {
        get => _processedRecords;
        set
        {
            _processedRecords = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessedRecords)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstimatedTimeRemaining)));
        }
    }

    public double NumberOfRecords
    {
        get => _numberOfRecords;
        set
        {
            _numberOfRecords = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberOfRecords)));
        }
    }

    public string EstimatedTimeRemaining
    {
        get
        {
            var timeElapsed = (DateTime.Now - _startTime).TotalMinutes;
            var processedRatio = _numberOfRecords / _processedRecords;
            if (_numberOfRecords > 0 && _numberOfRecords == _processedRecords)
            {
                return "Complete";
            }
            else if (double.IsInfinity(processedRatio) || double.IsNaN(processedRatio))
            {
                return "";
            }

            var timeRemaining = TimeSpan.FromMinutes((processedRatio * timeElapsed) - timeElapsed);
            return $"{(long)timeRemaining.TotalHours:00}" +
                $":{timeRemaining.Minutes:00}" +
                $":{timeRemaining.Seconds:00} remains";
        }
    }

    internal Task GetOTPData()
    {
        var update = new ProgressUpdate();
        _startTime = DateTime.Now;
        update.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ProgressUpdate.Current):
                    ProcessedRecords = update.Current;
                    return;
                case nameof(ProgressUpdate.Total):
                    NumberOfRecords = update.Total;
                    return;
            }
        }; 
        IsRunning = true;
        return CreateFeatures.AppendOTPData(
            _serverName, _tripFilePath, _outputFilePath, _otpServerThreads, update
            ).ContinueWith((_) =>
            {
                IsRunning = false;
            });

    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

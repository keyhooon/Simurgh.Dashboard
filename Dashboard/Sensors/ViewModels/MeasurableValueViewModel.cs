using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Simurgh.Dashboard.Sensors.Models;

namespace Simurgh.Dashboard.Sensors.ViewModels;

/// <summary>
/// Observable ViewModel wrapping <see cref="MeasurableValueEntity"/> with bidirectional property synchronization,
/// automated UI presentation formatting, alarm state computation, and dynamic segment placeholder calculation.
/// </summary>
public sealed partial class MeasurableValueViewModel : ObservableObject, IDisposable
{
    private readonly MeasurableValueEntity _model;
    private bool _isSyncingFromModel;
    private bool _disposedValue;

    #region Static Alarm Brushes

    private static readonly SolidColorBrush NormalBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#00E676")!;
    private static readonly SolidColorBrush WarningBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FFAB00")!;
    private static readonly SolidColorBrush CriticalBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FF1744")!;

    static MeasurableValueViewModel()
    {
        // Permanently freeze static singletons for cross-thread access and rendering performance optimization
        if (NormalBrush.CanFreeze) NormalBrush.Freeze();
        if (WarningBrush.CanFreeze) WarningBrush.Freeze();
        if (CriticalBrush.CanFreeze) CriticalBrush.Freeze();
    }

    #endregion

    #region Constructor & Initialization

    public MeasurableValueViewModel(MeasurableValueEntity model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;

        // Initialize ViewModel properties from Model snapshot
        SyncAllFromModel();

        // Subscribe to Model INotifyPropertyChanged for telemetry mutation propagation
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    #endregion

    #region Model Accessor

    /// <summary>
    /// Gets the underlying domain entity instance.
    /// </summary>
    public MeasurableValueEntity Model => _model;

    #endregion

    #region Observable Domain Properties

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private SensorType _type = SensorType.Temperature;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _valueFormat = "000.0;-00.0";

    [ObservableProperty]
    private string _defaultFormattedValue = "---.-";

    /// <summary>
    /// Seven-segment ghost background mask string (e.g. "888.8" or "888") 
    /// dynamically calculated based on the positive section of <see cref="ValueFormat"/>.
    /// </summary>
    [ObservableProperty]
    private string _placeholderValue = "888.8";

    [ObservableProperty]
    private double? _lowWarningThreshold;

    [ObservableProperty]
    private double? _highWarningThreshold;

    [ObservableProperty]
    private double? _lowCriticalThreshold;

    [ObservableProperty]
    private double? _highCriticalThreshold;

    [ObservableProperty]
    private Brush _digitBrush = Brushes.Tomato;

    [ObservableProperty]
    private Brush _placeholderBrush = Brushes.DarkSlateGray;

    [ObservableProperty]
    private double _realValue = double.NaN;

    [ObservableProperty]
    private string _formattedValue = "---.-";

    [ObservableProperty]
    private bool _isInWarning;

    [ObservableProperty]
    private bool _isInCritical;

    [ObservableProperty]
    private DateTimeOffset _lastUpdated = DateTimeOffset.UtcNow;

    #endregion

    #region UI Presentation Computed Properties

    /// <summary>
    /// Combined display string: e.g. "024.5 °C" or "---.- %".
    /// </summary>
    public string DisplayText => string.IsNullOrWhiteSpace(Unit)
        ? FormattedValue
        : $"{FormattedValue} {Unit}";

    /// <summary>
    /// Indicates whether the channel is currently in any abnormal threshold state.
    /// </summary>
    public bool HasAlarm => IsInWarning || IsInCritical;

    /// <summary>
    /// Contextual alarm brush based on domain entity severity evaluation.
    /// </summary>
    public Brush StatusBrush => IsInCritical
        ? CriticalBrush
        : IsInWarning
            ? WarningBrush
            : NormalBrush;

    /// <summary>
    /// Human-readable alarm text badge for tooltips or status bars.
    /// </summary>
    public string StatusDescription => IsInCritical
        ? "Critical"
        : IsInWarning
            ? "Warning"
            : "Normal";

    #endregion

    #region Property Changed Interceptors (ViewModel -> Model Propagation)

    partial void OnIndexChanged(int value)
    {
        if (!_isSyncingFromModel && _model.Index != value)
            _model.Index = value;
    }

    partial void OnLabelChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Label != value)
            _model.Label = value;
    }

    partial void OnTypeChanged(SensorType value)
    {
        if (!_isSyncingFromModel && _model.Type != value)
            _model.Type = value;
    }

    partial void OnUnitChanged(string value)
    {
        if (!_isSyncingFromModel && _model.Unit != value)
            _model.Unit = value;

        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnValueFormatChanged(string value)
    {
        // Automatically recalculate ghost segment placeholder mask whenever format changes
        PlaceholderValue = CalculatePlaceholderValue(value);

        if (!_isSyncingFromModel && _model.ValueFormat != value)
            _model.ValueFormat = value;
    }

    partial void OnDefaultFormattedValueChanged(string value)
    {
        if (!_isSyncingFromModel && _model.DefaultFormattedValue != value)
            _model.DefaultFormattedValue = value;
    }

    partial void OnLowWarningThresholdChanged(double? value)
    {
        if (!_isSyncingFromModel && _model.LowWarningThreshold != value)
            _model.LowWarningThreshold = value;
    }

    partial void OnHighWarningThresholdChanged(double? value)
    {
        if (!_isSyncingFromModel && _model.HighWarningThreshold != value)
            _model.HighWarningThreshold = value;
    }

    partial void OnLowCriticalThresholdChanged(double? value)
    {
        if (!_isSyncingFromModel && _model.LowCriticalThreshold != value)
            _model.LowCriticalThreshold = value;
    }

    partial void OnHighCriticalThresholdChanged(double? value)
    {
        if (!_isSyncingFromModel && _model.HighCriticalThreshold != value)
            _model.HighCriticalThreshold = value;
    }

    partial void OnDigitBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel && !Equals(_model.DigitBrush, value))
            _model.DigitBrush = value;
    }

    partial void OnPlaceholderBrushChanged(Brush value)
    {
        if (!_isSyncingFromModel && !Equals(_model.PlaceholderBrush, value))
            _model.PlaceholderBrush = value;
    }

    partial void OnRealValueChanged(double value)
    {
        if (!_isSyncingFromModel && !EqualityComparer<double>.Default.Equals(_model.RealValue, value))
            _model.RealValue = value;
    }

    partial void OnFormattedValueChanged(string value)
    {
        if (!_isSyncingFromModel && _model.FormattedValue != value)
            _model.FormattedValue = value;

        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnIsInWarningChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.IsInWarning != value)
            _model.IsInWarning = value;

        NotifyAlarmStateChanged();
    }

    partial void OnIsInCriticalChanged(bool value)
    {
        if (!_isSyncingFromModel && _model.IsInCritical != value)
            _model.IsInCritical = value;

        NotifyAlarmStateChanged();
    }

    partial void OnLastUpdatedChanged(DateTimeOffset value)
    {
        if (!_isSyncingFromModel && _model.LastUpdated != value)
            _model.LastUpdated = value;
    }

    private void NotifyAlarmStateChanged()
    {
        OnPropertyChanged(nameof(HasAlarm));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusDescription));
    }

    #endregion

    #region Model Event Handlers (Model -> ViewModel Propagation)

    /// <summary>
    /// Propagates property modifications initiated on the model back to the view model.
    /// </summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _isSyncingFromModel = true;
        try
        {
            switch (e.PropertyName)
            {
                case nameof(MeasurableValueEntity.Index):
                    Index = _model.Index;
                    break;
                case nameof(MeasurableValueEntity.Label):
                    Label = _model.Label;
                    break;
                case nameof(MeasurableValueEntity.Type):
                    Type = _model.Type;
                    break;
                case nameof(MeasurableValueEntity.Unit):
                    Unit = _model.Unit;
                    OnPropertyChanged(nameof(DisplayText));
                    break;
                case nameof(MeasurableValueEntity.ValueFormat):
                    ValueFormat = _model.ValueFormat;
                    PlaceholderValue = CalculatePlaceholderValue(_model.ValueFormat);
                    break;
                case nameof(MeasurableValueEntity.DefaultFormattedValue):
                    DefaultFormattedValue = _model.DefaultFormattedValue;
                    break;
                case nameof(MeasurableValueEntity.LowWarningThreshold):
                    LowWarningThreshold = _model.LowWarningThreshold;
                    break;
                case nameof(MeasurableValueEntity.HighWarningThreshold):
                    HighWarningThreshold = _model.HighWarningThreshold;
                    break;
                case nameof(MeasurableValueEntity.LowCriticalThreshold):
                    LowCriticalThreshold = _model.LowCriticalThreshold;
                    break;
                case nameof(MeasurableValueEntity.HighCriticalThreshold):
                    HighCriticalThreshold = _model.HighCriticalThreshold;
                    break;
                case nameof(MeasurableValueEntity.DigitBrush):
                    DigitBrush = _model.DigitBrush;
                    break;
                case nameof(MeasurableValueEntity.PlaceholderBrush):
                    PlaceholderBrush = _model.PlaceholderBrush;
                    break;
                case nameof(MeasurableValueEntity.RealValue):
                    RealValue = _model.RealValue;
                    break;
                case nameof(MeasurableValueEntity.FormattedValue):
                    FormattedValue = _model.FormattedValue;
                    OnPropertyChanged(nameof(DisplayText));
                    break;
                case nameof(MeasurableValueEntity.IsInWarning):
                    IsInWarning = _model.IsInWarning;
                    NotifyAlarmStateChanged();
                    break;
                case nameof(MeasurableValueEntity.IsInCritical):
                    IsInCritical = _model.IsInCritical;
                    NotifyAlarmStateChanged();
                    break;
                case nameof(MeasurableValueEntity.LastUpdated):
                    LastUpdated = _model.LastUpdated;
                    break;
                case null:
                case "":
                    // Null or empty property name indicates bulk update of all properties
                    SyncAllFromModel();
                    break;
            }
        }
        finally
        {
            _isSyncingFromModel = false;
        }
    }

    /// <summary>
    /// Synchronizes all observable properties from the current model state.
    /// </summary>
    private void SyncAllFromModel()
    {
        _isSyncingFromModel = true;
        try
        {
            Index = _model.Index;
            Label = _model.Label;
            Type = _model.Type;
            Unit = _model.Unit;
            ValueFormat = _model.ValueFormat;
            DefaultFormattedValue = _model.DefaultFormattedValue;
            PlaceholderValue = CalculatePlaceholderValue(_model.ValueFormat);
            LowWarningThreshold = _model.LowWarningThreshold;
            HighWarningThreshold = _model.HighWarningThreshold;
            LowCriticalThreshold = _model.LowCriticalThreshold;
            HighCriticalThreshold = _model.HighCriticalThreshold;
            DigitBrush = _model.DigitBrush;
            PlaceholderBrush = _model.PlaceholderBrush;
            RealValue = _model.RealValue;
            FormattedValue = _model.FormattedValue;
            IsInWarning = _model.IsInWarning;
            IsInCritical = _model.IsInCritical;
            LastUpdated = _model.LastUpdated;

            OnPropertyChanged(nameof(DisplayText));
            NotifyAlarmStateChanged();
        }
        finally
        {
            _isSyncingFromModel = false;
        }
    }

    #endregion

    #region Formatting & Placeholder Calculation Helpers

    /// <summary>
    /// Calculates the ghost/inactive seven-segment background string (e.g. "888.8" or "888")
    /// based on the positive pattern section of the format specifier.
    /// </summary>
    public static string CalculatePlaceholderValue(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "888.8";
        }

        // Extract positive portion if semicolons exist (e.g. "000.0;-00.0" -> "000.0")
        var positiveSection = format.Split(';')[0];

        var chars = positiveSection.Select(c => c switch
        {
            '0' or '#' => '8',
            '.' => '.',
            ',' => ',',
            _ => c
        }).ToArray();

        return new string(chars);
    }

    #endregion

    #region IDisposable

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Unsubscribe to prevent memory leaks via model event retainment
                _model.PropertyChanged -= OnModelPropertyChanged;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

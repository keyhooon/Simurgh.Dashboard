using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Simurgh.Dashboard.Sensors.Options;

namespace Simurgh.Dashboard.Sensors.Models;

/// <summary>
/// Domain model representing sensor identity, threshold configurations, and dynamic telemetry snapshots.
/// Implements <see cref="INotifyPropertyChanged"/> with optimized field-backing,
/// value equality guards, and thread-safe frozen brush handling.
/// </summary>
public sealed class MeasurableValueEntity : INotifyPropertyChanged
{
    #region Constants & Fallbacks

    private const string DefaultValueFormat = "000.0;-00.0";
    private const string FallbackFormattedValue = "---.-";

    private static readonly SolidColorBrush DefaultDigitBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#FF7878")!;
    private static readonly SolidColorBrush DefaultPlaceholderBrush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString("#2D263238")!;

    static MeasurableValueEntity()
    {
        // Permanently freeze static default singletons for cross-thread access and rendering pipeline performance
        if (DefaultDigitBrush.CanFreeze) DefaultDigitBrush.Freeze();
        if (DefaultPlaceholderBrush.CanFreeze) DefaultPlaceholderBrush.Freeze();
    }

    #endregion

    #region Backing Fields

    private int _index;
    private string _label = string.Empty;
    private SensorType _type = SensorType.Temperature;
    private string _unit = string.Empty;
    private string _valueFormat = DefaultValueFormat;
    private string _defaultFormattedValue = FallbackFormattedValue;
    private double? _lowWarningThreshold;
    private double? _highWarningThreshold;
    private double? _lowCriticalThreshold;
    private double? _highCriticalThreshold;
    private Brush _digitBrush = DefaultDigitBrush;
    private Brush _placeholderBrush = DefaultPlaceholderBrush;

    private double _realValue = double.NaN;
    private string _formattedValue = FallbackFormattedValue;
    private bool _isInWarning;
    private bool _isInCritical;
    private DateTimeOffset _lastUpdated = DateTimeOffset.UtcNow;

    private ushort? _modbusAddress;
    private ModbusRegisterType _registerType = ModbusRegisterType.HoldingRegister;
    private ModbusDataType _dataType = ModbusDataType.UInt16;
    private double _scaleFactor = 1.0;
    private double _offset;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region 1. Positional Identity

    /// <summary>
    /// Channel index matching hardware slot or channel offset.
    /// </summary>
    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    #endregion

    #region 2. Configuration State

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>
    /// Sensor category / physical metric classification.
    /// </summary>
    public SensorType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// Unit descriptor string for visual display (e.g. °C, %, Pa).
    /// </summary>
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    /// <summary>
    /// Numerical formatting specifier applied to raw telemetry data (e.g. "000.0;-00.0", "000;-00").
    /// </summary>
    public string ValueFormat
    {
        get => _valueFormat;
        set
        {
            if (SetProperty(ref _valueFormat, value))
            {
                _defaultFormattedValue = GenerateDefaultFormattedValue(value);
                OnPropertyChanged(nameof(DefaultFormattedValue));
                // Re-evaluate format when pattern changes at runtime
                EvaluateState(_realValue);
            }
        }
    }

    /// <summary>
    /// Fallback formatted string displayed before valid telemetry arrives (e.g. "---.-").
    /// </summary>
    public string DefaultFormattedValue
    {
        get => _defaultFormattedValue;
        set => SetProperty(ref _defaultFormattedValue, value);
    }

    /// <summary>
    /// Lower boundary threshold triggering warning alarm state.
    /// </summary>
    public double? LowWarningThreshold
    {
        get => _lowWarningThreshold;
        set => SetProperty(ref _lowWarningThreshold, value);
    }

    /// <summary>
    /// Upper boundary threshold triggering warning alarm state.
    /// </summary>
    public double? HighWarningThreshold
    {
        get => _highWarningThreshold;
        set => SetProperty(ref _highWarningThreshold, value);
    }

    /// <summary>
    /// Lower boundary threshold triggering critical alarm state.
    /// </summary>
    public double? LowCriticalThreshold
    {
        get => _lowCriticalThreshold;
        set => SetProperty(ref _lowCriticalThreshold, value);
    }

    /// <summary>
    /// Upper boundary threshold triggering critical alarm state.
    /// </summary>
    public double? HighCriticalThreshold
    {
        get => _highCriticalThreshold;
        set => SetProperty(ref _highCriticalThreshold, value);
    }

    /// <summary>
    /// Active telemetry digit rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush DigitBrush
    {
        get => _digitBrush;
        set
        {
            var frozen = FreezeOrFallback(value, DefaultDigitBrush);
            SetProperty(ref _digitBrush, frozen);
        }
    }

    /// <summary>
    /// Inactive placeholder segment rendering brush. Ensures frozen state on assignment.
    /// </summary>
    public Brush PlaceholderBrush
    {
        get => _placeholderBrush;
        set
        {
            var frozen = FreezeOrFallback(value, DefaultPlaceholderBrush);
            SetProperty(ref _placeholderBrush, frozen);
        }
    }

    #endregion

    #region 3. Real-Time Telemetry State

    /// <summary>
    /// Raw unformatted numerical telemetry value received from hardware/data stream.
    /// </summary>
    public double RealValue
    {
        get => _realValue;
        set => SetProperty(ref _realValue, value);
    }

    /// <summary>
    /// Render-ready formatted string representation of the raw value.
    /// </summary>
    public string FormattedValue
    {
        get => _formattedValue;
        set => SetProperty(ref _formattedValue, value);
    }

    /// <summary>
    /// Indicates whether the active value falls into warning threshold range.
    /// </summary>
    public bool IsInWarning
    {
        get => _isInWarning;
        set => SetProperty(ref _isInWarning, value);
    }

    /// <summary>
    /// Indicates whether the active value violates critical threshold range.
    /// </summary>
    public bool IsInCritical
    {
        get => _isInCritical;
        set => SetProperty(ref _isInCritical, value);
    }

    /// <summary>
    /// UTC timestamp of the most recent telemetry sample update.
    /// </summary>
    public DateTimeOffset LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    #endregion

    #region 4. Modbus Channel Mapping

    /// <summary>
    /// Gets or sets the Modbus register address (zero-based protocol address).
    /// A null value means this channel is not mapped to any Modbus register.
    /// </summary>
    public ushort? ModbusAddress
    {
        get => _modbusAddress;
        set => SetProperty(ref _modbusAddress, value);
    }

    /// <summary>
    /// Gets or sets the Modbus register area used for polling (Holding, Input, Coil, DiscreteInput).
    /// </summary>
    public ModbusRegisterType RegisterType
    {
        get => _registerType;
        set => SetProperty(ref _registerType, value);
    }

    /// <summary>
    /// Gets or sets the raw wire data type of the register (Int16, UInt16, Float32, ...).
    /// Determines how many registers are read and how the raw bytes are decoded.
    /// </summary>
    public ModbusDataType DataType
    {
        get => _dataType;
        set => SetProperty(ref _dataType, value);
    }

    /// <summary>
    /// Gets or sets the multiplicative factor converting raw register values into engineering units.
    /// </summary>
    public double ScaleFactor
    {
        get => _scaleFactor;
        set => SetProperty(ref _scaleFactor, value);
    }

    /// <summary>
    /// Gets or sets the additive offset applied after scaling (engineeringValue = raw * scale + offset).
    /// </summary>
    public double Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value);
    }

    /// <summary>
    /// Gets a value indicating whether this channel is bound to a Modbus register and should be polled.
    /// </summary>
    public bool HasModbusMapping => _modbusAddress.HasValue;

    #endregion

    #region Constructors

    /// <summary>
    /// Default parameterless constructor for object initializers and deserializers.
    /// </summary>
    public MeasurableValueEntity()
    {
    }

    /// <summary>
    /// Parameterized constructor with positional hardware index and optional configuration options.
    /// </summary>
    public MeasurableValueEntity(int index, MeasurableValueOptions? initialOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
        _lastUpdated = DateTimeOffset.UtcNow;

        if (initialOptions is not null)
        {
            ApplyConfiguration(initialOptions);
        }
    }

    /// <summary>
    /// Full parameterized constructor for domain instantiation including Modbus wire mappings.
    /// </summary>
    public MeasurableValueEntity(
        int index,
        SensorType type,
        string? unit,
        string? valueFormat = DefaultValueFormat,
        string? label = null,
        double? lowWarningThreshold = null,
        double? highWarningThreshold = null,
        double? lowCriticalThreshold = null,
        double? highCriticalThreshold = null,
        Brush? digitBrush = null,
        Brush? placeholderBrush = null,
        ushort? modbusAddress = null,
        ModbusRegisterType registerType = ModbusRegisterType.HoldingRegister,
        ModbusDataType dataType = ModbusDataType.Int16,
        double scaleFactor = 1.0,
        double offset = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
        _type = type;
        _unit = unit ?? string.Empty;
        _valueFormat = string.IsNullOrWhiteSpace(valueFormat) ? DefaultValueFormat : valueFormat;
        _label = label ?? string.Empty;
        _lowWarningThreshold = lowWarningThreshold;
        _highWarningThreshold = highWarningThreshold;
        _lowCriticalThreshold = lowCriticalThreshold;
        _highCriticalThreshold = highCriticalThreshold;
        _digitBrush = FreezeOrFallback(digitBrush, DefaultDigitBrush);
        _placeholderBrush = FreezeOrFallback(placeholderBrush, DefaultPlaceholderBrush);

        // Modbus mappings
        _modbusAddress = modbusAddress;
        _registerType = registerType;
        _dataType = dataType;
        _scaleFactor = scaleFactor;
        _offset = offset;

        _lastUpdated = DateTimeOffset.UtcNow;
    }

    #endregion

    #region Domain Operations

    /// <summary>
    /// Performs in-place mutation of configuration parameters directly from <see cref="MeasurableValueOptions"/>.
    /// </summary>
    public bool ApplyConfiguration(MeasurableValueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Type = options.Type;
        Unit = options.Unit ?? string.Empty;
        ValueFormat = string.IsNullOrWhiteSpace(options.ValueFormat) ? DefaultValueFormat : options.ValueFormat;
        DefaultFormattedValue = GenerateDefaultFormattedValue(ValueFormat);

        LowWarningThreshold = options.LowWarningThreshold;
        HighWarningThreshold = options.HighWarningThreshold;
        LowCriticalThreshold = options.LowCriticalThreshold;
        HighCriticalThreshold = options.HighCriticalThreshold;

        DigitBrush = ParseBrushOrDefault(options.DigitColorHex, DefaultDigitBrush);
        PlaceholderBrush = ParseBrushOrDefault(options.PlaceholderColorHex, DefaultPlaceholderBrush);

        // Modbus channel mapping (address, register area, wire data type, and scaling)
        ModbusAddress = options.ModbusAddress;
        RegisterType = options.RegisterType;
        DataType = options.DataType;
        ScaleFactor = options.ScaleFactor;
        Offset = options.Offset;

        EvaluateState(RealValue);

        return true;
    }

    /// <summary>
    /// Updates real-time telemetry state, recalculates alarm status, and formats presentation string.
    /// </summary>
    public void UpdateTelemetry(double newRealValue, DateTimeOffset? timestamp = null)
    {
        EvaluateState(newRealValue);
        LastUpdated = timestamp ?? DateTimeOffset.UtcNow;
    }

    private void EvaluateState(double value)
    {
        RealValue = value;

        if (double.IsNaN(value))
        {
            FormattedValue = DefaultFormattedValue;
            IsInCritical = false;
            IsInWarning = false;
            return;
        }

        try
        {
            FormattedValue = value.ToString(ValueFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            FormattedValue = value.ToString(CultureInfo.InvariantCulture);
        }

        var isLowCritical = LowCriticalThreshold.HasValue && value <= LowCriticalThreshold.Value;
        var isHighCritical = HighCriticalThreshold.HasValue && value >= HighCriticalThreshold.Value;
        IsInCritical = isLowCritical || isHighCritical;

        if (IsInCritical)
        {
            IsInWarning = false;
        }
        else
        {
            var isLowWarning = LowWarningThreshold.HasValue && value <= LowWarningThreshold.Value;
            var isHighWarning = HighWarningThreshold.HasValue && value >= HighWarningThreshold.Value;
            IsInWarning = isLowWarning || isHighWarning;
        }
    }

    #endregion

    #region Property Changed Helpers

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Helper Methods

    private static Brush ParseBrushOrDefault(string? hexColor, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return fallback;
        }

        try
        {
            if (ColorConverter.ConvertFromString(hexColor) is Color color)
            {
                var brush = new SolidColorBrush(color);
                if (brush.CanFreeze) brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Fallback gracefully on malformed hex strings
        }

        return fallback;
    }

    private static Brush FreezeOrFallback(Brush? brush, Brush fallback)
    {
        var target = brush ?? fallback;

        if (target.IsFrozen)
        {
            return target;
        }

        if (target.CanFreeze)
        {
            var cloned = target.Clone();
            cloned.Freeze();
            return cloned;
        }

        return fallback;
    }

    /// <summary>
    /// Derives the placeholder display pattern (e.g. "---.-") directly from the format specifier
    /// by preserving separators and replacing numeric placeholders with dashes.
    /// </summary>
    private static string GenerateDefaultFormattedValue(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "---.-";
        }

        // If format contains section separators (e.g. "000.0;-00.0"), extract the positive section
        var positiveFormat = format.Split(';')[0];

        var chars = positiveFormat.Select(c => c switch
        {
            '0' or '#' => '-',
            '.' => '.',
            _ => c
        }).ToArray();

        return new string(chars);
    }


    #endregion
}

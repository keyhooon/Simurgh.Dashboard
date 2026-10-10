// Path: Simurgh.Dashboard.Sensors.Options/MeasurableValueOptions.cs

using Simurgh.Dashboard.Sensors.Models;

namespace Simurgh.Dashboard.Sensors.Options;

/// <summary>
/// Configuration model for a single measurable channel within a sensor module.
/// </summary>
public sealed class MeasurableValueOptions
{
    /// <summary>
    /// Metric type categorization (e.g., Temperature, Humidity, Pressure).
    /// </summary>
    public SensorType Type { get; set; } = SensorType.Temperature;

    /// <summary>
    /// Physical engineering unit representation (e.g., "°C", "%", "bar").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Default display string fallback when no valid reading is present.
    /// </summary>
    public string ValueFormat { get; set; } = "F1";

    /// <summary>
    /// Hex color string for active digits (e.g., "#FFFF7878" or "#FF7878").
    /// Null or empty uses the default theme color.
    /// </summary>
    public string? DigitColorHex { get; set; }

    /// <summary>
    /// Hex color string for inactive placeholder segments (e.g., "#2D263238").
    /// Null or empty uses the default theme color.
    /// </summary>
    public string? PlaceholderColorHex { get; set; }

    /// <summary>
    /// Optional low warning threshold value.
    /// </summary>
    public double? LowWarningThreshold { get; set; }

    /// <summary>
    /// Optional high warning threshold value.
    /// </summary>
    public double? HighWarningThreshold { get; set; }

    /// <summary>
    /// Optional low critical alarm threshold value.
    /// </summary>
    public double? LowCriticalThreshold { get; set; }

    /// <summary>
    /// Optional high critical alarm threshold value.
    /// </summary>
    public double? HighCriticalThreshold { get; set; }

    // ---------------------------------------------------------------------------
    // Modbus Addressing & Telemetry Mapping
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the target Modbus register address / offset (e.g., 0 for %MW0 or %IW0).
    /// Null if the channel is not mapped to Modbus.
    /// </summary>
    public ushort? ModbusAddress { get; set; }

    /// <summary>
    /// Gets or sets the Modbus register table type (Holding Register, Input Register, etc.).
    /// Defaults to HoldingRegister (%MW).
    /// </summary>
    public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

    /// <summary>
    /// Gets or sets the interpreted data type over Modbus registers.
    /// Defaults to Int16.
    /// </summary>
    public ModbusDataType DataType { get; set; } = ModbusDataType.Int16;

    /// <summary>
    /// Optional multiplier to convert raw integer register values into engineering units (e.g., 0.1 for 254 -> 25.4 °C).
    /// Defaults to 1.0.
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Optional additive offset applied after scaling: (Raw * ScaleFactor) + Offset.
    /// Defaults to 0.0.
    /// </summary>
    public double Offset { get; set; } = 0.0;
}

public enum ModbusDataType
{
    Int16,       // 1 Register (short)
    UInt16,      // 1 Register (ushort)
    Int32,       // 2 Registers (int)
    Float32,     // 2 Registers (float / IEEE 754)
    Boolean      // 1 Bit (Coil / Discrete)
}

public enum ModbusRegisterType
{
    HoldingRegister = 3,  // 40001+ (%MW / %QW)
    InputRegister = 4,    // 30001+ (%IW)
    Coil = 1,             // 00001+ (%QX)
    DiscreteInput = 2     // 10001+ (%IX)
}

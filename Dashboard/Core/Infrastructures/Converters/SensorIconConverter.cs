using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Simurgh.Dashboard.Sensors.Models;

namespace Simurgh.Dashboard.Core.Infrastructures.Converters;

/// <summary>
/// Converts a <see cref="SensorType"/> enum value to its corresponding vector <see cref="Geometry"/> resource defined in Icons.xaml.
/// Implements <see cref="MarkupExtension"/> to enable direct in-place XAML markup binding without boilerplate StaticResource declarations.
/// </summary>
[ValueConversion(typeof(SensorType), typeof(Geometry))]
public sealed class SensorIconConverter : MarkupExtension, IValueConverter
{
    private static SensorIconConverter? _instance;

    /// <summary>
    /// Returns the singleton instance of the converter for XAML markup extension evaluation.
    /// </summary>
    /// <param name="serviceProvider">A service provider helper that can provide services for the markup extension.</param>
    /// <returns>The static instance of <see cref="SensorIconConverter"/>.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) => _instance ??= new SensorIconConverter();

    /// <summary>
    /// Evaluates the incoming <see cref="SensorType"/> and resolves the matching vector <see cref="Geometry"/> from application resources.
    /// </summary>
    /// <param name="value">The <see cref="SensorType"/> enum value passed by the data binding target.</param>
    /// <param name="targetType">The type of the binding target property (expected <see cref="Geometry"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>A resolved <see cref="Geometry"/> resource or the default fallback <c>IconSensor</c>.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SensorType sensorType)
            return Application.Current.TryFindResource("IconSensor") as Geometry;

        var resourceKey = sensorType switch
        {
            // =========================================================================
            // SYSTEM & GENERIC TRANSDUCERS (0 - 255)
            // =========================================================================
            SensorType.Generic => "IconSensor",
            SensorType.Unknown => "IconSensor",

            // =========================================================================
            // ENVIRONMENTAL & HVAC CLIMATE TELEMETRY (100 - 199)
            // =========================================================================
            SensorType.Temperature => "IconTemperature",
            SensorType.Humidity => "IconHumidity",
            SensorType.Pressure => "IconPressure",
            SensorType.Illuminance => "IconIlluminance",
            SensorType.AirQuality => "IconAirQuality",
            SensorType.CarbonDioxide => "IconCarbonDioxide",
            SensorType.TotalVolatileOrganicCompounds => "IconTotalVolatileOrganicCompounds",
            SensorType.RoomDifferentialPressure => "IconRoomDifferentialPressure",
            SensorType.FilterDifferentialPressure => "IconHepaFilterClogPressure",

            // =========================================================================
            // MEDICAL, LIFE-SUPPORT & PHYSIOLOGICAL (200 - 299)
            // =========================================================================
            SensorType.PulseOximetry => "IconPulseOximetry",
            SensorType.Ecg => "IconEcg",
            SensorType.BloodPressure => "IconBloodPressure",
            SensorType.Capnography => "IconCapnography",
            SensorType.MedicalGasPressure => "IconMedicalGasPressure",
            SensorType.AnestheticAgent => "IconAnestheticAgent",
            SensorType.MedicalGasO2 => "IconMedicalGasO2",
            SensorType.MedicalGasCO2 => "IconMedicalGasCO2",
            SensorType.MedicalGas10Bar => "IconMedicalGas10Bar",
            SensorType.MedicalGasN2O => "IconMedicalGasN2O",
            SensorType.MedicalVacuum => "IconMedicalVacuum",

            // =========================================================================
            // KINEMATIC, SPATIAL & FLUID DYNAMICS (300 - 399)
            // =========================================================================
            SensorType.Presence => "IconPresence",
            SensorType.Vibration => "IconVibration",
            SensorType.PositionEncoder => "IconPositionEncoder",
            SensorType.LoadCell => "IconLoadCell",
            SensorType.Proximity => "IconProximity",
            SensorType.FlowRate => "IconFlowRate",

            // =========================================================================
            // ELECTRICAL POWER & MEDICAL SAFETY DIAGNOSTICS (400 - 499)
            // =========================================================================
            SensorType.Voltage => "IconVoltage",
            SensorType.Current => "IconCurrent",
            SensorType.Power => "IconPower",
            SensorType.LeakageCurrent => "IconLeakageCurrent",
            SensorType.InsulationResistance => "IconInsulationResistance",
            SensorType.LineIsolationMonitor => "IconLineIsolationMonitor",

            // =========================================================================
            // IONIZING RADIATION & NUCLEAR MEDICINE (500 - 599)
            // =========================================================================
            SensorType.RadiationDosimetry => "IconRadiationDosimetry",

            // Default fallback for any undefined enum states
            _ => "IconSensor"
        };

        return Application.Current.TryFindResource(resourceKey) as Geometry
               ?? Application.Current.TryFindResource("IconSensor") as Geometry;
    }

    /// <summary>
    /// One-way conversion assertion; converting a vector Geometry back to a SensorType is unsupported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
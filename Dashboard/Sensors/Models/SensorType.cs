using System.ComponentModel;

namespace Simurgh.Dashboard.Sensors.Models;

/// <summary>
/// Defines the complete telemetry transducer and biomedical sensor taxonomy.
/// Encoded as an unsigned 16-bit integer (ushort) partitioned into deterministic clinical and physical sub-domains.
/// </summary>
public enum SensorType : ushort
{
    // =========================================================================
    // 0x0000 - 0x00FF: SYSTEM & GENERIC TRANSDUCERS (0 - 255)
    // =========================================================================

    [Description("Unknown / Unmapped Sensor")]
    Unknown = 0,

    [Description("Generic Transducer")]
    Generic = 1,

    // =========================================================================
    // 0x0100 - 0x01FF: ENVIRONMENTAL & HVAC CLIMATE TELEMETRY (100 - 199)
    // =========================================================================

    [Description("Ambient Temperature (ISO 7000-0534)")]
    Temperature = 100,

    [Description("Relative Humidity")]
    Humidity = 101,

    [Description("Atmospheric / Differential Static Pressure (ISO 7000-1141)")]
    Pressure = 102,

    [Description("Ambient / Surgical Illuminance Lux")]
    Illuminance = 103,

    [Description("HVAC Air Cleanliness / Particle Count (ISO 14644)")]
    AirQuality = 104,

    [Description("NDIR Carbon Dioxide Gas (CO2)")]
    CarbonDioxide = 105,

    [Description("Volatile Organic Compounds (VOC)")]
    TotalVolatileOrganicCompounds = 106,

    [Description("Differential Room Overpressure / Cascade Barrier")]
    RoomDifferentialPressure = 107,

    [Description("Heppa Filter Differential Pressure")]
    FilterDifferentialPressure = 108,

    // =========================================================================
    // 0x0200 - 0x02FF: MEDICAL, LIFE-SUPPORT & PHYSIOLOGICAL (200 - 299)
    // (IEC 60601-1-8 / ISO 80601 / IEC TR 60878)
    // =========================================================================

    [Description("Pulse Oximetry Plethysmograph (SpO2 - ISO 80601-2-61)")]
    PulseOximetry = 200,

    [Description("Electrocardiogram Diagnostic Lead (ECG - IEC 60601-2-27)")]
    Ecg = 201,

    [Description("Non-Invasive Blood Pressure (NIBP - ISO 7000-2443)")]
    BloodPressure = 202,

    [Description("Expired Carbon Dioxide Capnography (EtCO2 - ISO 80601-2-55)")]
    Capnography = 203,

    [Description("Medical Gas Pipeline Supply Pressure (ISO 7396-1)")]
    MedicalGasPressure = 204,

    [Description("Anesthetic Agent Vaporizer Concentration (ISO 80601-2-13)")]
    AnestheticAgent = 205,

    [Description("Medical Oxygen Delivery (O2)")]
    MedicalGasO2 = 206,

    [Description("Carbon Monoxide Gas (CO)")]
    MedicalGasCO2 = 207,

    [Description("Instrument Air / High Pressure (10 BAR)")]
    MedicalGas10Bar = 208,

    [Description("Nitrous Oxide Anaesthetic Gas (N2O)")]
    MedicalGasN2O = 209,

    [Description("Medical Vacuum / Suction Pipeline (ISO 7396-1)")]
    MedicalVacuum = 210,

    // =========================================================================
    // 0x0300 - 0x03FF: KINEMATIC, SPATIAL & FLUID DYNAMICS (300 - 399)
    // =========================================================================

    [Description("Passive Infrared Presence / Motion")]
    Presence = 300,

    [Description("3-Axis Harmonic Vibration / Accelerometer")]
    Vibration = 301,

    [Description("Rotary / Optical Position Encoder")]
    PositionEncoder = 302,

    [Description("Strain Gauge Force / Load Cell")]
    LoadCell = 303,

    [Description("Inductive / Optical Limit Proximity Sensor")]
    Proximity = 304,

    [Description("Volumetric / Mass Flow Rate Transducer (ISO 5167)")]
    FlowRate = 305,

    // =========================================================================
    // 0x0400 - 0x04FF: ELECTRICAL POWER & MEDICAL SAFETY DIAGNOSTICS (400 - 499)
    // (IEC 60417 / IEC 62353 / IEC 60601-1)
    // =========================================================================

    [Description("AC/DC Voltage Hazard (IEC 60417-5036)")]
    Voltage = 400,

    [Description("Primary / Secondary Load Current")]
    Current = 401,

    [Description("Active / Apparent Electric Power")]
    Power = 402,

    [Description("Medical Earth / Enclosure / Patient Leakage Current (IEC 62353)")]
    LeakageCurrent = 403,

    [Description("Dielectric Barrier Insulation Resistance Megohmmeter (IEC 60601-1)")]
    InsulationResistance = 404,

    [Description("Line Isolation Monitor (LIM / Isolated Power System IT-Network)")]
    LineIsolationMonitor = 405,

    // =========================================================================
    // 0x0500 - 0x05FF: IONIZING RADIATION & NUCLEAR MEDICINE (500 - 599)
    // (ISO 361 / ISO 7010-W003)
    // =========================================================================

    [Description("Ionizing Radiation Dosimetry Rate (ISO 361)")]
    RadiationDosimetry = 500,



}

namespace Simurgh.Dashboard.Sensors.Options;

/// <summary>
/// Root configuration section mapping for sensors within appsettings.json.
/// Example section: "SensorsSettings"
/// </summary>
public sealed class SensorsOptions
{
    public const string SectionName = "SensorsSettings";

    /// <summary>
    /// List of configured sensor modules.
    /// </summary>
    public List<SensorOptions> Sensors { get; set; } = [];

    public ModbusMasterOptions ModbusMaster { get; set; } = new ModbusMasterOptions();
}
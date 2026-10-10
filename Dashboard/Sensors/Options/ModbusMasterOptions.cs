// Path: Simurgh.Dashboard.Sensors.Options/ModbusMasterOptions.cs

#nullable enable

using System.ComponentModel.DataAnnotations;

namespace Simurgh.Dashboard.Sensors.Options;

/// <summary>
/// Configuration options for the Modbus TCP client connection to OpenPLC.
/// </summary>
public sealed class ModbusMasterOptions
{
    public const string SectionName = "ModbusMaster";

    /// <summary>
    /// IP address or hostname of the OpenPLC Modbus server. Defaults to localhost.
    /// </summary>
    [Required]
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Modbus TCP port. Standard default is 502.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 502;

    /// <summary>
    /// Unit Identifier (Slave Address) for Modbus TCP frames. Defaults to 1.
    /// </summary>
    [Range(0, 255)]
    public byte UnitIdentifier { get; set; } = 1;

    /// <summary>
    /// Communication timeout in milliseconds for read and write operations.
    /// </summary>
    [Range(100, 30000)]
    public int ConnectTimeoutMs { get; set; } = 2000;
}
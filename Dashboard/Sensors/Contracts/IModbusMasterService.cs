// Path: Simurgh.Dashboard.Sensors.Contracts/IModbusMasterService.cs

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Simurgh.Dashboard.Sensors.Contracts;

/// <summary>
/// Service contract for industrial Modbus TCP client communication.
/// Handles connection lifecycle, reconnect resilience, and table read/write operations.
/// </summary>
public interface IModbusMasterService : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the underlying Modbus TCP client is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Asynchronously establishes a TCP connection to the configured Modbus slave (OpenPLC).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously disconnects and releases the underlying TCP connection and streams.
    /// </summary>
    Task DisconnectAsync();

    #region Discrete Inputs & Coils (1-bit)

    /// <summary>
    /// Reads contiguous coils (Function Code 01) from the remote slave.
    /// </summary>
    /// <param name="startingAddress">Zero-based starting coil address (0x0000 to 0xFFFF).</param>
    /// <param name="count">Number of contiguous coils to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of boolean values representing coil states.</returns>
    Task<bool[]> ReadCoilsAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads contiguous discrete inputs (Function Code 02) from the remote slave.
    /// </summary>
    /// <param name="startingAddress">Zero-based starting input address (0x0000 to 0xFFFF).</param>
    /// <param name="count">Number of contiguous discrete inputs to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of boolean values representing discrete input states.</returns>
    Task<bool[]> ReadDiscreteInputsAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single coil (Function Code 05) to the remote slave.
    /// </summary>
    /// <param name="address">Zero-based coil address.</param>
    /// <param name="value">Boolean value to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken cancellationToken = default);

    #endregion

    #region Registers (16-bit)

    /// <summary>
    /// Reads contiguous holding registers (Function Code 03) from the remote slave.
    /// </summary>
    /// <param name="startingAddress">Zero-based starting register address (0x0000 to 0xFFFF).</param>
    /// <param name="count">Number of 16-bit registers to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of 16-bit unsigned integers representing register contents.</returns>
    Task<ushort[]> ReadHoldingRegistersAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads contiguous input registers (Function Code 04) from the remote slave.
    /// </summary>
    /// <param name="startingAddress">Zero-based starting register address (0x0000 to 0xFFFF).</param>
    /// <param name="count">Number of 16-bit input registers to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of 16-bit unsigned integers representing input register contents.</returns>
    Task<ushort[]> ReadInputRegistersAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single 16-bit holding register (Function Code 06) to the remote slave.
    /// </summary>
    /// <param name="address">Zero-based register address.</param>
    /// <param name="value">16-bit value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple contiguous 16-bit holding registers (Function Code 16) to the remote slave.
    /// </summary>
    /// <param name="startingAddress">Zero-based starting register address.</param>
    /// <param name="values">Array of 16-bit values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteMultipleRegistersAsync(ushort startingAddress, ushort[] values, CancellationToken cancellationToken = default);

    #endregion
}

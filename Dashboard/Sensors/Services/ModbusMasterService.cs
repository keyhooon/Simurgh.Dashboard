// Path: Simurgh.Dashboard.Sensors.Services/ModbusMasterService.cs

#nullable enable

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simurgh.Dashboard.Sensors.Contracts;
using Simurgh.Dashboard.Sensors.Options;

namespace Simurgh.Dashboard.Sensors.Services;

/// <summary>
/// Thread-safe industrial implementation of <see cref="IModbusMasterService"/> wrapping FluentModbus.
/// Manages socket lifecycle and synchronizes concurrent command dispatches to OpenPLC.
/// </summary>
public sealed class ModbusMasterService : IModbusMasterService
{
    private readonly ModbusTcpClient _client;
    private readonly IOptionsMonitor<ModbusMasterOptions> _optionsMonitor;
    private readonly ILogger<ModbusMasterService> _logger;

    // FluentModbus TCP client is not inherently thread-safe for parallel writes/reads on a single stream
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isDisposed;

    public ModbusMasterService(
        IOptionsMonitor<ModbusMasterOptions> optionsMonitor,
        ILogger<ModbusMasterService> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client = new ModbusTcpClient
        {
            ReadTimeout = _optionsMonitor.CurrentValue.ConnectTimeoutMs,
            WriteTimeout = _optionsMonitor.CurrentValue.ConnectTimeoutMs
        };
    }

    /// <inheritdoc />
    public bool IsConnected => !_isDisposed && _client.IsConnected;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected)
            {
                return;
            }

            var options = _optionsMonitor.CurrentValue;
            _logger.LogInformation("Connecting to OpenPLC Modbus server at {Host}:{Port} (UnitId: {UnitId})...",
                options.Host, options.Port, options.UnitIdentifier);

            // Run TCP connect in background task to avoid blocking caller thread
            await Task.Run(() =>
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(options.Host), options.Port);
                _client.Connect(endpoint, ModbusEndianness.BigEndian);
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully connected to OpenPLC Modbus server.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish Modbus connection to OpenPLC.");
            _client.Disconnect();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client.IsConnected)
            {
                _logger.LogInformation("Disconnecting from OpenPLC Modbus server.");
                _client.Disconnect();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    #region Discrete Inputs & Coils (1-bit)

    /// <inheritdoc />
    public async Task<bool[]> ReadCoilsAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            return await Task.Run(() =>
            {
                var span = _client.ReadCoils(unitId, startingAddress, count);
                var result = new bool[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = (span[i / 8] & (1 << (i % 8))) != 0;
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            return await Task.Run(() =>
            {
                var span = _client.ReadDiscreteInputs(unitId, startingAddress, count);
                var result = new bool[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = (span[i / 8] & (1 << (i % 8))) != 0;
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            await Task.Run(() => _client.WriteSingleCoil(unitId, address, value), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Registers (16-bit)

    /// <inheritdoc />
    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            return await Task.Run(() =>
            {
                var span = _client.ReadHoldingRegisters<ushort>(unitId, startingAddress, count);
                return span.ToArray();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ushort[]> ReadInputRegistersAsync(ushort startingAddress, ushort count, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            return await Task.Run(() =>
            {
                var span = _client.ReadInputRegisters<ushort>(unitId, startingAddress, count);
                return span.ToArray();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken = default)
    {
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            await Task.Run(() => _client.WriteSingleRegister(unitId, address, value), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteMultipleRegistersAsync(ushort startingAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ThrowIfNotReady();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var unitId = _optionsMonitor.CurrentValue.UnitIdentifier;
            await Task.Run(() => _client.WriteMultipleRegisters(unitId, startingAddress, values), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion

    #region Guard & Dispose

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void ThrowIfNotReady()
    {
        ThrowIfDisposed();
        if (!_client.IsConnected)
        {
            throw new InvalidOperationException("Modbus master client is not connected to OpenPLC server.");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
            _client.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception suppressed during synchronous dispose.");
        }
        finally
        {
            _gate.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await Task.Run(() =>
            {
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }
                _client.Dispose();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception suppressed during asynchronous dispose.");
        }
        finally
        {
            _gate.Dispose();
        }
    }

    #endregion
}

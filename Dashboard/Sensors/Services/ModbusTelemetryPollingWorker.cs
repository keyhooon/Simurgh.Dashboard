// Path: Simurgh.Dashboard.Sensors.Services/ModbusTelemetryPollingWorker.cs

#nullable enable

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simurgh.Dashboard.Sensors.Contracts;
using Simurgh.Dashboard.Sensors.Models;
using Simurgh.Dashboard.Sensors.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Simurgh.Dashboard.Sensors.Services;

/// <summary>
/// Background worker responsible for polling disparate Modbus registers
/// and pushing decoded engineering telemetry to the domain via <see cref="ISensorController"/>.
/// Implements per-sensor fault isolation: a timeout or communication failure on one sensor
/// degrades only that sensor's state (Warning -> Error -> Offline) while healthy sensors continue polling.
/// </summary>
public sealed class ModbusTelemetryPollingWorker(
    ISensorAccessor sensorAccessor,
    ISensorController sensorController,
    IModbusMasterService modbusMasterService,
    ILogger<ModbusTelemetryPollingWorker> logger)
    : BackgroundService
{
    private readonly ISensorAccessor _sensorAccessor = sensorAccessor ?? throw new ArgumentNullException(nameof(sensorAccessor));
    private readonly ISensorController _sensorController = sensorController ?? throw new ArgumentNullException(nameof(sensorController));
    private readonly IModbusMasterService _modbusMasterService = modbusMasterService ?? throw new ArgumentNullException(nameof(modbusMasterService));
    private readonly ILogger<ModbusTelemetryPollingWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Number of consecutive failed poll cycles before a sensor is escalated from Error to Offline.
    /// </summary>
    private const int OfflineFailureThreshold = 3;

    /// <summary>
    /// Number of consecutive failed poll cycles before a sensor is escalated to Warning.
    /// </summary>
    private const int WarningFailureThreshold = 1;

    /// <summary>
    /// Tracks consecutive per-sensor failure counts. Access is confined to the polling loop (single consumer).
    /// </summary>
    private readonly Dictionary<int, int> _consecutiveFailures = [];

    private bool _connectionWasEstablished;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Modbus Telemetry Polling Worker...");
        await SetAllSensorsStateAsync(ModuleState.Online, stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_modbusMasterService.IsConnected)
                {
                    await _modbusMasterService.ConnectAsync(stoppingToken).ConfigureAwait(false);
                }

                // Connection-level failure (whole bus down): degrade all sensors to Offline
                await PollAndIngestAsync(stoppingToken).ConfigureAwait(false);

                _connectionWasEstablished = true;
                await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Modbus polling cycle failed at connection level. Retrying in {Delay}s...", ReconnectDelay.TotalSeconds);

                // Whole-bus failure: mark every sensor Offline only if a previous healthy connection existed
                if (_connectionWasEstablished)
                {
                    await SetAllSensorsStateAsync(ModuleState.Offline, stoppingToken).ConfigureAwait(false);
                    _connectionWasEstablished = false;
                    _consecutiveFailures.Clear();
                }

                await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Modbus Telemetry Polling Worker stopped.");
    }

    /// <summary>
    /// Traverses all registered sensor entities and their mapped Modbus channels to poll and ingest telemetry.
    /// Failures are scoped per-sensor: each sensor's consecutive failure count drives its individual
    /// state escalation (Online -> Warning -> Error -> Offline) via <see cref="ISensorController"/>.
    /// </summary>
    private async Task PollAndIngestAsync(CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow;

        for (var sensorIndex = 0; sensorIndex < _sensorAccessor.Count; sensorIndex++)
        {
            var sensor = _sensorAccessor.FindByIndex(sensorIndex);
            if (sensor is null)
            {
                continue;
            }

            var sensorFailed = false;
            var failureReason = string.Empty;

            for (var channelIndex = 0; channelIndex < sensor.Count; channelIndex++)
            {
                var channel = sensor[channelIndex];

                // Skip channels that are not mapped to Modbus registers
                if (!channel.HasModbusMapping || !channel.ModbusAddress.HasValue)
                {
                    continue;
                }

                try
                {
                    var rawValue = await ReadChannelRawValueAsync(channel, ct).ConfigureAwait(false);
                    var engineeringValue = (rawValue * channel.ScaleFactor) + channel.Offset;
                                        await _sensorController.IngestTelemetryAsync(new SensorTelemetryParams
                    {
                        SensorIndex = sensorIndex,
                        ChannelIndex = channelIndex,
                        Value = engineeringValue,
                        Timestamp = timestamp
                    }, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Graceful shutdown: propagate without touching sensor state
                    throw;
                }
                catch (Exception ex)
                {
                    sensorFailed = true;
                    failureReason = ex.Message;
                    _logger.LogDebug(ex, "Failed to read Modbus register for Sensor [{SensorIndex}], Channel [{ChannelIndex}] at Address {Address}.",
                        sensorIndex, channelIndex, channel.ModbusAddress.Value);
                }
            }

            // Per-sensor state machine update based on the cycle outcome
            await UpdateSensorStateOnOutcomeAsync(sensorIndex, sensorFailed, failureReason, timestamp, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Escalates or recovers an individual sensor's state based on its consecutive failure count.
    /// Recovery resets immediately to Online on the first successful cycle.
    /// </summary>
    private async Task UpdateSensorStateOnOutcomeAsync(int sensorIndex, bool failed, string failureReason, DateTimeOffset timestamp, CancellationToken ct)
    {
        if (failed)
        {
            // Increment and clamp the consecutive failure counter
            _consecutiveFailures.TryGetValue(sensorIndex, out var currentCount);
            _consecutiveFailures[sensorIndex] = currentCount + 1;
            var failureCount = _consecutiveFailures[sensorIndex];

            // Determine escalated state from failure count
            var newState = failureCount switch
            {
                >= OfflineFailureThreshold => ModuleState.Offline,
                >= WarningFailureThreshold => ModuleState.Warning,
                _ => ModuleState.Online
            };

            // Offline is a terminal latched state per threshold; only report while below or equal to threshold transitions
            if (failureCount <= OfflineFailureThreshold)
            {
                _logger.LogWarning("Sensor [{SensorIndex}] degraded to {State} after {Count} consecutive failed cycles. Reason: {Reason}",
                    sensorIndex, newState, failureCount, failureReason);

                await _sensorController.UpdateStateAsync(new SensorStateParams
                {
                    SensorIndex = sensorIndex,
                    State = newState,
                    Timestamp = timestamp
                }, ct).ConfigureAwait(false);
            }
        }
        else if (_consecutiveFailures.ContainsKey(sensorIndex))
        {
            // Successful cycle after prior failures: recover to Online and clear the counter
            _consecutiveFailures.Remove(sensorIndex);

            _logger.LogInformation("Sensor [{SensorIndex}] recovered to Online after previous failures.", sensorIndex);

            await _sensorController.UpdateStateAsync(new SensorStateParams
            {
                SensorIndex = sensorIndex,
                State = ModuleState.Online,
                Timestamp = timestamp
            }, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads and parses the raw numerical value based on channel configuration and register table.
    /// </summary>
    private async Task<double> ReadChannelRawValueAsync(MeasurableValueEntity channel, CancellationToken ct)
    {
        var address = channel.ModbusAddress!.Value;

        switch (channel.RegisterType)
        {
            case ModbusRegisterType.HoldingRegister:
                {
                    if (channel.DataType is ModbusDataType.Int16)
                    {
                        var registers = await _modbusMasterService.ReadHoldingRegistersAsync(address, 1, ct).ConfigureAwait(false);
                        return registers.Length > 0 ? (short)registers[0] : 0.0;
                    }
                    else if (channel.DataType is ModbusDataType.UInt16)
                    {
                        var registers = await _modbusMasterService.ReadHoldingRegistersAsync(address, 1, ct).ConfigureAwait(false);
                        return registers.Length > 0 ? (ushort)registers[0] : 0.0;
                    }
                    else if (channel.DataType is ModbusDataType.Float32)
                    {
                        var registers = await _modbusMasterService.ReadHoldingRegistersAsync(address, 2, ct).ConfigureAwait(false);
                        if (registers.Length < 2)
                        {
                            throw new InvalidOperationException("Insufficient registers returned for Float32 decoding.");
                        }

                        Span<byte> bytes = stackalloc byte[4];
                        BitConverter.TryWriteBytes(bytes[..2], registers[0]);
                        BitConverter.TryWriteBytes(bytes[2..], registers[1]);
                        return BitConverter.ToSingle(bytes);
                    }

                    throw new NotSupportedException($"Data type '{channel.DataType}' is not supported for HoldingRegister reads.");
                }

            case ModbusRegisterType.InputRegister:
                {
                    if (channel.DataType is ModbusDataType.Int16)
                    {
                        var registers = await _modbusMasterService.ReadInputRegistersAsync(address, 1, ct).ConfigureAwait(false);
                        return registers.Length > 0 ? (short)registers[0] : 0.0;
                    }
                    else if (channel.DataType is ModbusDataType.UInt16)
                    {
                        var registers = await _modbusMasterService.ReadInputRegistersAsync(address, 1, ct).ConfigureAwait(false);
                        return registers.Length > 0 ? (ushort)registers[0] : 0.0;
                    }

                    throw new NotSupportedException($"Data type '{channel.DataType}' is not supported for InputRegister reads.");
                }

            case ModbusRegisterType.Coil:
                {
                    var coils = await _modbusMasterService.ReadCoilsAsync(address, 1, ct).ConfigureAwait(false);
                    return (coils.Length > 0 && coils[0]) ? 1.0 : 0.0;
                }

            case ModbusRegisterType.DiscreteInput:
                {
                    var inputs = await _modbusMasterService.ReadDiscreteInputsAsync(address, 1, ct).ConfigureAwait(false);
                    return (inputs.Length > 0 && inputs[0]) ? 1.0 : 0.0;
                }

            default:
                throw new NotSupportedException($"Modbus register type '{channel.RegisterType}' is not supported.");
        }
    }

    /// <summary>
    /// Broadcasts a state change to all available sensor modules (used for whole-bus failures).
    /// </summary>
    private async Task SetAllSensorsStateAsync(ModuleState state, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow;
        for (var i = 0; i < _sensorAccessor.Count; i++)
        {
            await _sensorController.UpdateStateAsync(new SensorStateParams
            {
                SensorIndex = i,
                State = state,
                Timestamp = timestamp
            }, ct).ConfigureAwait(false);
        }
    }
}

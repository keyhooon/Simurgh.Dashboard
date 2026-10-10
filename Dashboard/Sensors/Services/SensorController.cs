#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Simurgh.Dashboard.Core.Ipc;
using Simurgh.Dashboard.Sensors.Contracts;
using Simurgh.Dashboard.Sensors.Models;

namespace Simurgh.Dashboard.Sensors.Services;

/// <summary>
/// Orchestrates sensor state transitions, telemetry ingestion, and configuration.
/// Exposes direct asynchronous methods suitable for RPC dispatching and local domain consumption.
/// </summary>
public sealed class SensorController(ISensorAccessor sensorAccessor) : ISensorController
{
    private readonly ISensorAccessor _sensorAccessor = sensorAccessor ?? throw new ArgumentNullException(nameof(sensorAccessor));

    /// <inheritdoc />
    public Task UpdateStateAsync(SensorStateParams parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var sensor = _sensorAccessor.FindByIndex(parameters.SensorIndex);
        if (sensor is null)
        {
            return Task.CompletedTask;
        }

        sensor.UpdateState(parameters.State, parameters.Timestamp);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task IngestTelemetryAsync(SensorTelemetryParams parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var sensor = _sensorAccessor.FindByIndex(parameters.SensorIndex);
        if (sensor is null || sensor.State == ModuleState.Offline)
        {
            return Task.CompletedTask;
        }

        if (parameters.ChannelIndex >= 0 && parameters.ChannelIndex < sensor.Count)
        {
            sensor.IngestChannelTelemetry(parameters.ChannelIndex, parameters.Value, parameters.Timestamp);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyConfigurationAsync(SensorConfigParams parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Options is null)
        {
            return Task.CompletedTask;
        }

        var sensor = _sensorAccessor.FindByIndex(parameters.SensorIndex);
        if (sensor is null)
        {
            return Task.CompletedTask;
        }

        sensor.ApplyConfiguration(parameters.Options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetTelemetryAsync(SensorIndexParams parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var sensor = _sensorAccessor.FindByIndex(parameters.SensorIndex);
        if (sensor is null)
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < sensor.Count; i++)
        {
            sensor.IngestChannelTelemetry(i, default, now);
        }

        sensor.UpdateState(ModuleState.Offline, now);
        return Task.CompletedTask;
    }
}

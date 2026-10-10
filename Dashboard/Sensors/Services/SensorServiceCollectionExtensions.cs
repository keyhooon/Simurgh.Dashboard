using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simurgh.Dashboard.Sensors.Contracts;
using Simurgh.Dashboard.Sensors.Options;
using Simurgh.Dashboard.Sensors.ViewModels;
using System.Configuration;

namespace Simurgh.Dashboard.Sensors.Services;

/// <summary>
/// Service collection extension methods for registering the sensor subsystem,
/// configuration monitors, domain accessors, background services, and MVVM presentation models.
/// </summary>
public static class SensorServiceCollectionExtensions
{


    /// <summary>
    /// Registers all components of the Sensor Subsystem into the <see cref="IServiceCollection"/>.
    /// Configures <see cref="SensorsOptions"/> from the configuration tree, binds positional state accessors,
    /// background operational ingest services, and UI presentation ViewModels.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The root application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddSensorSubsystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Configuration & Options Mapping (Hot-Reloadable via IOptionsMonitor<SensorsOptions>)
        services
            .AddOptions<SensorsOptions>()
            .BindConfiguration(SensorsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();


        // 2. Core Accessor Layer (Thread-Safe Single Instance)
        services.TryAddSingleton<ISensorAccessor, SensorAccessor>();
        services.TryAddSingleton<ISensorController, SensorController>();
        // 4. MVVM Presentation Layer
        // Root ViewModel as Singleton for primary dashboard life-cycle
        services.TryAddSingleton<SensorsRootViewModel>();

        // Transient factory registration for sub-viewmodels if resolved directly via DI
        services.TryAddTransient<Func<Models.SensorEntity, SensorViewModel>>(sp =>
            entity => new SensorViewModel(entity));

        services.TryAddTransient<Func<Models.MeasurableValueEntity, MeasurableValueViewModel>>(sp =>
            entity => new MeasurableValueViewModel(entity));

        // 1. Modbus Options
        services.AddOptions<ModbusMasterOptions>()
            .Bind(configuration.GetSection(ModbusMasterOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 2. Modbus Master Service (Registered as Singleton)
        services.AddSingleton<IModbusMasterService, ModbusMasterService>();

        // 3. Telemetry Polling Background Worker
        services.AddHostedService<ModbusTelemetryPollingWorker>();

        return services;
    }

    
}

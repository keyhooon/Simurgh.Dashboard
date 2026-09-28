// Path: Simurgh.Dashboard/HealthCheck/Services/ServiceCollectionExtensions.cs

using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simurgh.Dashboard.Watchdog.ViewModels;
using Simurgh.Watchdog.Agent;
using Watchdog.Agent.Host;

namespace Simurgh.Dashboard.Watchdog.Services;

/// <summary>
/// Service registration extensions for bridging Watchdog Agent with WPF Dashboard UI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string DefaultSectionName = WatchdogAgentOptions.SectionName;

    /// <summary>
    /// Registers the Watchdog Agent pipeline with the self-contained WPF Management Handler,
    /// UI pulse monitoring, and dashboard view models using IConfiguration.
    /// </summary>
    public static IServiceCollection AddSimurghWpfWatchdog(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegisterWpfPrerequisites(services);

        // Registers WatchdogAgentOptions, pipeline, proxies, update services, and WpfAgentManagementHandler
        services.AddSimurghWatchdogAgent<WpfAgentManagementHandler>(configuration, sectionName);

        RegisterDashboardComponents(services);

        return services;
    }

    /// <summary>
    /// Registers the Watchdog Agent pipeline with the self-contained WPF Management Handler,
    /// UI pulse monitoring, and dashboard view models using programmatic options configuration.
    /// </summary>
    public static IServiceCollection AddSimurghWpfWatchdog(
        this IServiceCollection services,
        Action<WatchdogAgentOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        RegisterWpfPrerequisites(services);

        // Registers WatchdogAgentOptions, pipeline, proxies, update services, and WpfAgentManagementHandler
        services.AddSimurghWatchdogAgent<WpfAgentManagementHandler>(configureOptions);

        RegisterDashboardComponents(services);

        return services;
    }

    /// <summary>
    /// Ensures UI Thread Dispatcher is available in the DI container.
    /// </summary>
    private static void RegisterWpfPrerequisites(IServiceCollection services)
    {
        services.TryAddSingleton(_ =>
            System.Windows.Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher);
    }

    /// <summary>
    /// Registers Dashboard UI-specific dependencies and forwards management handlers.
    /// </summary>
    private static void RegisterDashboardComponents(IServiceCollection services)
    {

        // Dashboard Status Indicator UI ViewModel
        services.TryAddSingleton<WatchdogStatusIndicatorViewModel>();
    }
}

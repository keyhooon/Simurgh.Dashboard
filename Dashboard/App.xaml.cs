using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using Simurgh.Dashboard.Clock.Options;
using Simurgh.Dashboard.Clock.Services.Weather;
using Simurgh.Dashboard.Clock.ViewModels;
using Simurgh.Dashboard.Core.Ipc;
using Simurgh.Dashboard.HealthCheck.Services;
using Simurgh.Dashboard.Patient.Services;
using Simurgh.Dashboard.RssFeed.Services;
using Simurgh.Dashboard.RssFeed.ViewModels;
using Simurgh.Dashboard.Sensors.Services;
using Simurgh.Dashboard.Timers.Contracts;
using Simurgh.Dashboard.Timers.Services;
using Simurgh.Dashboard.Timers.ViewModels;
using Simurgh.Watchdog.Agent;
using System;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Velopack;

namespace Simurgh.Dashboard
{
    /// <summary>
    /// Core application class responsible for bootstrapping the Simurgh Dashboard kiosk.
    /// Manages the Microsoft.Extensions.Hosting lifecycle, DI pipeline, NLog infrastructure, and global fault tolerance.
    /// </summary>
    public partial class App : Application
    {
        // Global named mutex to enforce a single running instance across sessions.
        private const string SingleInstanceMutexName = @"Global\Simurgh.Dashboard";

        // Must remain alive for the entire process lifetime to prevent duplicate instances.
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;

        // Static NLog logger initialized before DI/IHost pipeline for early boot diagnostics.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        // Host instance encapsulating DI, configuration, logging, and all registered IHostedService workers.
        private IHost? _host;

        /// <summary>
        /// Global service provider access point for legacy components or dynamic resolution.
        /// </summary>
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        // =========================================================================
        // ENTRY POINT
        // =========================================================================
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Setup early NLog configuration from BaseDirectory if present
                var nlogConfigFile = Path.Combine(AppContext.BaseDirectory, "NLog.config");
                if (File.Exists(nlogConfigFile))
                {
                    LogManager.Setup().LoadConfigurationFromFile(nlogConfigFile);
                }

                _logger.Info("==================================================");
                _logger.Info("Simurgh Dashboard process initialized (PID: {0})", Environment.ProcessId);
                _logger.Info("==================================================");

                // Run Velopack hooks as the very first instruction before WPF runtime
                VelopackApp.Build().Run();

                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Unhandled crash during application early initialization.");
                throw;
            }
            finally
            {
                // Ensure all logs are flushed to disk if the process terminates
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Boots the generic host, starts hosted background workers, and renders the main kiosk shell.
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Establish global fault tolerance and exception protection immediately.
            RegisterGlobalExceptionHooks();

            // -------------------------------------------------------------------------
            // SINGLE INSTANCE GUARD (MUTEX)
            // -------------------------------------------------------------------------
            if (!TryAcquireSingleInstanceMutex(SingleInstanceMutexName))
            {
                _logger.Warn("Simurgh Dashboard is already running. Fast terminating secondary instance.");
                Shutdown(0);
                return;
            }

            _logger.Info("Simurgh Dashboard starting up (IHost bootstrapping)...");

            try
            {
                // Build the generic host using HostApplicationBuilder (.NET 8/9 optimized pipeline).
                var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
                {
                    Args = e.Args,
                    ContentRootPath = AppContext.BaseDirectory
                });

                // Explicitly load configuration files with change monitoring.
                builder.Configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

                // Configure logging pipeline to route through NLog.
                builder.Logging.ClearProviders();
                builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                builder.Logging.AddNLog();

                // Register services, options, viewmodels, and workers.
                ConfigureServices(builder.Services, builder.Configuration);

                // Build the host container; triggers ValidateOnStart checks across all options.
                _host = builder.Build();
                ServiceProvider = _host.Services;

                _logger.Info("IHost successfully built. Starting hosted background services...");

                // StartAsync automatically resolves and executes ExecuteAsync on all IHostedService instances.
                await _host.StartAsync();

                _logger.Info("All hosted services started. Rendering MainWindow...");

                // Resolve and display the kiosk shell on the UI thread.
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Fatal error during application host startup — process will terminate.");

                MessageBox.Show(
                    $"Application failed to initialize host pipeline:\n\n{ex.Message}",
                    "Simurgh Dashboard — Boot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                LogManager.Shutdown();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Registers options, HTTP handlers, workers, views, and viewmodels into the DI container.
        /// </summary>
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // -------------------------------------------------------------------------
            // WINDOWS & UI SHELL
            // -------------------------------------------------------------------------
            services.AddSingleton<MainWindow>();

            // -------------------------------------------------------------------------
            // STRONGLY-TYPED OPTIONS WITH FAIL-FAST VALIDATION
            // -------------------------------------------------------------------------
            services
                .AddOptions<DigitalClockOptions>()
                .BindConfiguration(DigitalClockOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddOptions<KioskDisplayOptions>()
                .BindConfiguration(KioskDisplayOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<WatchdogAgentOptions>()
                .BindConfiguration(WatchdogAgentOptions.SectionName)
                .PostConfigure(options =>
                {
                    var entryAssembly = Assembly.GetEntryAssembly()?.GetName().Name;
                    var fallbackName = !string.IsNullOrWhiteSpace(entryAssembly)
                        ? entryAssembly
                        : AppDomain.CurrentDomain.FriendlyName;

                    if (string.IsNullOrWhiteSpace(options.AppId))
                    {
                        options.AppId = fallbackName;
                    }

                    if (string.IsNullOrWhiteSpace(options.PipeName))
                    {
                        options.PipeName = $"Simurgh.Watchdog.{options.AppId}";
                    }
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // -------------------------------------------------------------------------
            // BACKGROUND WORKERS & DOMAIN MODULES (Weather, Ticker, Notifications, Timers)
            // -------------------------------------------------------------------------
            services.AddRssTickerWorker(configuration);
            services.AddLocalNotificationService();
            services.AddWeatherServices(configuration);
            services.AddTimerWorkerServices(configuration);
            services.AddPatientDemographics(configuration);
            services.AddSensorSubsystem(configuration);

            services.AddSimurghWpfWatchdog(configuration);

            // -------------------------------------------------------------------------
            // VIEWMODELS (Stateful Singletons for Kiosk Lifecycle)
            // -------------------------------------------------------------------------
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<DigitalClockViewModel>();
        }

        /// <summary>
        /// Sets up comprehensive global exception traps for UI, AppDomain, and unobserved tasks.
        /// </summary>
        private void RegisterGlobalExceptionHooks()
        {
            // 1. UI Dispatcher Exceptions (Keeps the UI alive in kiosk mode)
            DispatcherUnhandledException += (s, e) =>
            {
                _logger.Error(e.Exception, "Unhandled UI Dispatcher Exception trapped. Prevented application crash.");
                e.Handled = true;
            };

            // 2. AppDomain Unhandled Exceptions (Non-UI threads)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    _logger.Fatal(ex, "AppDomain Unhandled Exception occurred. IsTerminating: {0}", e.IsTerminating);
                }
                else
                {
                    _logger.Fatal("AppDomain Unhandled Non-Exception Object: {0}", e.ExceptionObject);
                }
                LogManager.Flush();
            };

            // 3. Unobserved Task Exceptions (Async void / unawaited tasks)
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                _logger.Error(e.Exception, "Unobserved Task Exception trapped.");
                e.SetObserved();
            };
        }

        /// <summary>
        /// Performs graceful shutdown of the host, cancels workers, flushes log buffers,
        /// and releases the single-instance mutex.
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            _logger.Info("Simurgh Dashboard shutting down with exit code: {0}. Stopping IHost...", e.ApplicationExitCode);

            if (_host != null)
            {
                try
                {
                    // Gracefully stop all background services within a 5-second deadline.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _host.StopAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error occurred during graceful shutdown of IHost.");
                }
                finally
                {
                    _host.Dispose();
                    _host = null;
                }
            }

            ReleaseSingleInstanceMutex();

            _logger.Info("Simurgh Dashboard exited cleanly.");
            LogManager.Shutdown();

            base.OnExit(e);
        }

        /// <summary>
        /// Attempts to acquire a named system-wide mutex to enforce single-instance behavior.
        /// Returns false if another instance is already running.
        /// </summary>
        private bool TryAcquireSingleInstanceMutex(string mutexName)
        {
            try
            {
                _singleInstanceMutex = CreateOrOpenMutexWithWorldAcl(mutexName, out var createdNew);

                bool hasHandle;
                try
                {
                    // Zero-timeout wait: non-blocking acquisition attempt.
                    hasHandle = _singleInstanceMutex.WaitOne(0, false);
                }
                catch (AbandonedMutexException)
                {
                    // Prior instance terminated unexpectedly without releasing the mutex.
                    _logger.Warn("Abandoned mutex detected. Previous instance might have crashed. Continuing execution.");
                    hasHandle = true;
                }

                _ownsSingleInstanceMutex = hasHandle;

                if (!_ownsSingleInstanceMutex)
                {
                    return false;
                }

                _logger.Info("Single-instance mutex successfully acquired (CreatedNew: {0}, Name: {1}).", createdNew, mutexName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create or acquire single-instance mutex: {0}", mutexName);
                // Fail-safe default: allow the app to run if mutex subsystem throws an OS error
                return true;
            }
        }

        /// <summary>
        /// Releases the acquired mutex handle safely and disposes resources.
        /// </summary>
        private void ReleaseSingleInstanceMutex()
        {
            try
            {
                if (_singleInstanceMutex != null && _ownsSingleInstanceMutex)
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
            }
            catch (ApplicationException)
            {
                // Current thread did not hold the lock; safe to ignore during shutdown.
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unexpected error occurred while releasing single-instance mutex.");
            }
            finally
            {
                _ownsSingleInstanceMutex = false;
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
        }

        /// <summary>
        /// Creates or opens a named mutex configured with permissive ACL (Everyone + Current User FullControl).
        /// Compatible with both standard and Windows Kiosk/Service session switches.
        /// </summary>
        private static Mutex CreateOrOpenMutexWithWorldAcl(string name, out bool createdNew)
        {
            var security = new MutexSecurity();

            // Grant full control to Everyone (World SID).
            security.AddAccessRule(new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow));

            // Explicitly ensure the current user identity has full control.
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                security.AddAccessRule(new MutexAccessRule(
                    currentUser,
                    MutexRights.FullControl,
                    AccessControlType.Allow));
            }

            return MutexAcl.Create(
                initiallyOwned: false,
                name: name,
                createdNew: out createdNew,
                mutexSecurity: security);
        }
    }
}

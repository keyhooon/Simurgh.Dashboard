// ============================================================================
// File: KioskDisplayBehavior.cs
// Purpose: Highly Configurable, Generalized Kiosk Display Behavior (Expanded)
// ============================================================================

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using NLog;
using Simurgh.Dashboard.Core.Infrastructures.Native;

namespace Simurgh.Dashboard.Core.Infrastructures.Behaviors
{
    /// <summary>
    /// Expanded XAML Behavior. Supports targeting by connection technology or exact screen index.
    /// Manages DPI scaling safely, automatically handles visual tree restoration, and 
    /// includes robust fallbacks to ensure the Kiosk always launches successfully.
    /// </summary>
    public class KioskDisplayBehavior : Behavior<Window>
    {
        #region Fields

        // Static NLog logger matching App.xaml.cs bootstrapping conventions
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        // Caches the device name and its original orientation before the behavior modified it.
        // This is crucial for reverting the state locally if the window is closed but the app remains alive.
        private string _appliedDeviceName;
        private DisplayOrientation? _originalOrientation;

        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty TargetTechnologyProperty =
            DependencyProperty.Register(
                nameof(TargetTechnology),
                typeof(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY),
                typeof(KioskDisplayBehavior),
                new PropertyMetadata(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Hdmi));

        public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY TargetTechnology
        {
            get => (DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY)GetValue(TargetTechnologyProperty);
            set => SetValue(TargetTechnologyProperty, value);
        }

        public static readonly DependencyProperty TargetOrientationProperty =
            DependencyProperty.Register(
                nameof(TargetOrientation),
                typeof(DisplayOrientation),
                typeof(KioskDisplayBehavior),
                new PropertyMetadata(DisplayOrientation.Portrait));

        public DisplayOrientation TargetOrientation
        {
            get => (DisplayOrientation)GetValue(TargetOrientationProperty);
            set => SetValue(TargetOrientationProperty, value);
        }

        public static readonly DependencyProperty GpuSyncDelayMsProperty =
            DependencyProperty.Register(
                nameof(GpuSyncDelayMs),
                typeof(int),
                typeof(KioskDisplayBehavior),
                new PropertyMetadata(1000)); // 1 second is optimal for heavy WDDM transitions

        public int GpuSyncDelayMs
        {
            get => (int)GetValue(GpuSyncDelayMsProperty);
            set => SetValue(GpuSyncDelayMsProperty, value);
        }

        /// <summary>
        /// Allows explicit targeting by GDI name (e.g. "\\.\DISPLAY2") overriding the Technology property.
        /// Useful for systems with multiple identical ports (e.g. dual HDMI Kiosks).
        /// </summary>
        public static readonly DependencyProperty ExplicitDeviceNameProperty =
            DependencyProperty.Register(
                nameof(ExplicitDeviceName),
                typeof(string),
                typeof(KioskDisplayBehavior),
                new PropertyMetadata(string.Empty));

        public string ExplicitDeviceName
        {
            get => (string)GetValue(ExplicitDeviceNameProperty);
            set => SetValue(ExplicitDeviceNameProperty, value);
        }

        /// <summary>
        /// Determines whether the behavior should revert the screen orientation to its original state
        /// when the Window is closed. Set to false if the global App.xaml `DisplayConfigurationSnapshot`
        /// handles the application-wide reversion.
        /// </summary>
        public static readonly DependencyProperty RevertOnCloseProperty =
            DependencyProperty.Register(
                nameof(RevertOnClose),
                typeof(bool),
                typeof(KioskDisplayBehavior),
                new PropertyMetadata(true));

        public bool RevertOnClose
        {
            get => (bool)GetValue(RevertOnCloseProperty);
            set => SetValue(RevertOnCloseProperty, value);
        }

        #endregion

        #region Overrides

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnWindowLoaded;

            // We use 'Closed' instead of 'Unloaded' for Windows. 
            // 'Unloaded' can fire during theme changes or logical tree restructuring, 
            // whereas 'Closed' strictly means the window is being destroyed.
            AssociatedObject.Closed += OnWindowClosed;
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnWindowLoaded;
                AssociatedObject.Closed -= OnWindowClosed;
            }
            base.OnDetaching();
        }

        #endregion

        #region Event Handlers

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.Loaded -= OnWindowLoaded;

            // Ensure window is hidden/invisible during hardware transition to avoid flicker
            var originalOpacity = AssociatedObject.Opacity;
            AssociatedObject.Opacity = 0;

            await ApplyKioskTopologyAsync();

            AssociatedObject.Opacity = originalOpacity;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            AssociatedObject.Closed -= OnWindowClosed;

            // If RevertOnClose is true and we successfully tracked a modified device,
            // we safely restore the display orientation back to what it was before the window opened.
            if (RevertOnClose && !string.IsNullOrEmpty(_appliedDeviceName) && _originalOrientation.HasValue)
            {
                try
                {
                    MonitorHelper.SetOrientation(_appliedDeviceName, _originalOrientation.Value);

                    // Flush the GDI cache to prevent stale handles from affecting subsequent operations
                    MonitorHelper.ClearCache();
                    _logger.Info("Reverted display orientation for device '{0}' to {1}.", _appliedDeviceName, _originalOrientation.Value);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to revert display orientation on window close for device '{0}'.", _appliedDeviceName);
                }
            }
        }

        #endregion

        #region Private Methods

        private async Task ApplyKioskTopologyAsync()
        {
            _logger.Info("================ [APPLY KIOSK TOPOLOGY PIPELINE] ================");
            _logger.Info(
                "[Kiosk Config] ExplicitDeviceName='{0}', TargetTechnology={1}, TargetOrientation={2}, GpuSyncDelayMs={3}",
                ExplicitDeviceName, TargetTechnology, TargetOrientation, GpuSyncDelayMs);

            try
            {
                // Enumerate and log all connected display pipelines and GPU ports for diagnostic audit
                DisplayConfigHelper.LogActiveDisplaysAndGraphicsPorts();

                // Resolve device target priority: Explicit String > Technology Discovery > Primary Monitor
                _logger.Debug("[Topology Resolution] Resolving target device name...");
                string targetDeviceName = string.IsNullOrWhiteSpace(ExplicitDeviceName)
                    ? DisplayConfigHelper.GetMonitorDeviceNameByTechnology(TargetTechnology)
                    : ExplicitDeviceName;

                _logger.Info("[Topology Resolution] Resulting targetDeviceName='{0}'", targetDeviceName ?? "<NULL>");

                if (string.IsNullOrEmpty(targetDeviceName))
                {
                    // Full Fallback logic if technology is unplugged/unavailable
                    _logger.Warn("[Topology Fallback] Target device name not found or unplugged. Falling back to primary monitor bounds.");
                    var fallbackBounds = MonitorHelper.GetPrimaryMonitorBounds();
                    _logger.Info(
                        "[Topology Fallback] Primary Monitor Bounds: X={0}, Y={1}, W={2}, H={3}",
                        fallbackBounds.left, fallbackBounds.top, fallbackBounds.Width, fallbackBounds.Height);

                    ApplyBoundsToWindow(fallbackBounds);
                    _logger.Info("================ [KIOSK TOPOLOGY COMPLETED (FALLBACK)] ================\n");
                    return;
                }

                // Cache the current hardware state before making any modifications
                var currentSettings = MonitorHelper.GetCurrentSettings(targetDeviceName);
                _originalOrientation = (DisplayOrientation)currentSettings.dmDisplayOrientation;
                _appliedDeviceName = targetDeviceName;

                _logger.Info(
                    "[Hardware Query] Target='{0}', Current Resolution: {1}x{2} @ {3}Hz, Original Orientation={4}",
                    targetDeviceName, currentSettings.dmPelsWidth, currentSettings.dmPelsHeight, currentSettings.dmDisplayFrequency, _originalOrientation);

                _logger.Info("[Hardware Mutate] Applying new orientation '{0}' to device '{1}'...", TargetOrientation, targetDeviceName);
                bool rotationApplied = MonitorHelper.SetOrientation(targetDeviceName, TargetOrientation);
                _logger.Info("[Hardware Mutate] MonitorHelper.SetOrientation result: {0}", rotationApplied);

                if (rotationApplied && GpuSyncDelayMs > 0)
                {
                    // Asynchronously waiting for WM_DISPLAYCHANGE OS propagation
                    _logger.Info("[GPU Sync] Awaiting {0}ms for DWM / driver WM_DISPLAYCHANGE propagation...", GpuSyncDelayMs);
                    await Task.Delay(GpuSyncDelayMs);
                    _logger.Debug("[GPU Sync] Delay elapsed. Fetching updated layout bounds.");
                }

                var finalBounds = MonitorHelper.GetMonitorBoundsByName(targetDeviceName);
                _logger.Info(
                    "[Viewport Bounds] Target='{0}', Final Bounds: X={1}, Y={2}, W={3}, H={4}",
                    targetDeviceName, finalBounds.left, finalBounds.top, finalBounds.Width, finalBounds.Height);

                ApplyBoundsToWindow(finalBounds);
                _logger.Info("================ [KIOSK TOPOLOGY COMPLETED (SUCCESS)] ================\n");
            }
            catch (Exception ex)
            {
                // Safety net: ensure window is at least visible on primary screen if DWM faults
                _logger.Error(ex, "[Topology Exception] Critical failure during display topology application.");

                var safeBounds = MonitorHelper.GetPrimaryMonitorBounds();
                _logger.Warn(
                    "[Topology Safety Net] Applying safe fallback bounds: X={0}, Y={1}, W={2}, H={3}",
                    safeBounds.left, safeBounds.top, safeBounds.Width, safeBounds.Height);

                ApplyBoundsToWindow(safeBounds);
                _logger.Warn("================ [KIOSK TOPOLOGY COMPLETED (EXCEPTION RESCUE)] ================\n");
            }
        }

        /// <summary>
        /// Handles the complex math of mapping Windows API Physical Pixels to WPF Logical DIPs,
        /// ensuring the window perfectly fills the target monitor regardless of Windows Display Scaling.
        /// </summary>
        private void ApplyBoundsToWindow(MonitorHelper.RECT physicalBounds)
        {
            if (AssociatedObject == null) return;

            var hwndSource = PresentationSource.FromVisual(AssociatedObject) as HwndSource;

            if (hwndSource?.CompositionTarget != null)
            {
                Matrix transformToDevice = hwndSource.CompositionTarget.TransformToDevice;

                // Invert the matrix to convert Physical OS Pixels -> WPF Logical Coordinates
                Matrix transformToDip = transformToDevice;
                transformToDip.Invert();

                Point physicalTopLeft = new Point(physicalBounds.left, physicalBounds.top);
                Point logicalTopLeft = transformToDip.Transform(physicalTopLeft);

                // We only need to set Left/Top. Maximizing handles the Width/Height inherently.
                AssociatedObject.Left = logicalTopLeft.X;
                AssociatedObject.Top = logicalTopLeft.Y;
            }
            else
            {
                AssociatedObject.Left = physicalBounds.left;
                AssociatedObject.Top = physicalBounds.top;
            }

            // Force layout invalidation and remeasure by cycling WindowState
            if (AssociatedObject.WindowState == WindowState.Maximized)
            {
                AssociatedObject.WindowState = WindowState.Normal;
            }

            AssociatedObject.WindowStyle = WindowStyle.None;
            AssociatedObject.ResizeMode = ResizeMode.NoResize;
            AssociatedObject.WindowState = WindowState.Maximized;
        }

        #endregion
    }
}

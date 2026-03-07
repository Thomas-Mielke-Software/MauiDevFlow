using System.Diagnostics;
using Microsoft.Maui.Devices;

namespace MauiDevFlow.Agent.Core.Profiling;

public class RuntimeProfilerCollector : IProfilerCollector
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly ProfilerCapabilities _capabilities = new()
    {
        Platform = GetPlatformName(),
        ManagedMemorySupported = true,
        GcSupported = true,
        CpuPercentSupported = true,
        FpsSupported = true,
        FrameTimingsEstimated = true,
        ThreadCountSupported = true
    };

    private bool _running;
    private DateTime _lastSampleTimestampUtc;
    private TimeSpan _lastCpuTime;
    private int _sampleIntervalMs = 500;
    private double _estimatedFrameTimeMs = 1000d / 60d;
    private string _frameQuality = "estimated.default-60hz";

    public void Start(int intervalMs)
    {
        if (intervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalMs), "Sample interval must be > 0");

        _sampleIntervalMs = intervalMs;
        _lastSampleTimestampUtc = DateTime.UtcNow;
        (_estimatedFrameTimeMs, _frameQuality) = ResolveFrameEstimate();

        try
        {
            _process.Refresh();
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            || ex is NotSupportedException
            || ex is PlatformNotSupportedException)
        {
            _capabilities.CpuPercentSupported = false;
            _capabilities.ThreadCountSupported = false;
        }

        if (_capabilities.CpuPercentSupported)
        {
            try
            {
                _lastCpuTime = _process.TotalProcessorTime;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException
                || ex is NotSupportedException
                || ex is PlatformNotSupportedException)
            {
                _capabilities.CpuPercentSupported = false;
                _lastCpuTime = TimeSpan.Zero;
            }
        }

        _running = true;
    }

    public void Stop()
    {
        _running = false;
    }

    public bool TryCollect(out ProfilerSample sample)
    {
        sample = new ProfilerSample();
        if (!_running)
            return false;

        var now = DateTime.UtcNow;
        var elapsedMs = Math.Max(1d, (now - _lastSampleTimestampUtc).TotalMilliseconds);
        var effectiveElapsedMs = Math.Max(_sampleIntervalMs, elapsedMs);
        var lagRatio = effectiveElapsedMs / _sampleIntervalMs;
        var estimatedFrameTimeMs = _estimatedFrameTimeMs * lagRatio;
        var estimatedFps = estimatedFrameTimeMs > 0 ? 1000d / estimatedFrameTimeMs : (double?)null;
        var cpuPercent = TryReadCpuPercent(elapsedMs);
        var threadCount = TryReadThreadCount();

        sample = new ProfilerSample
        {
            TsUtc = now,
            Fps = estimatedFps,
            FrameTimeMsP50 = estimatedFrameTimeMs,
            FrameTimeMsP95 = estimatedFrameTimeMs,
            FrameQuality = $"{_frameQuality}.sampling-lag",
            ManagedBytes = GC.GetTotalMemory(false),
            Gc0 = GC.CollectionCount(0),
            Gc1 = GC.CollectionCount(1),
            Gc2 = GC.CollectionCount(2),
            CpuPercent = cpuPercent,
            ThreadCount = threadCount
        };

        _lastSampleTimestampUtc = now;
        return true;
    }

    public ProfilerCapabilities GetCapabilities() => _capabilities;

    private double? TryReadCpuPercent(double elapsedMs)
    {
        if (!_capabilities.CpuPercentSupported)
            return null;

        try
        {
            _process.Refresh();
            var cpuTime = _process.TotalProcessorTime;
            var cpuDeltaMs = (cpuTime - _lastCpuTime).TotalMilliseconds;
            _lastCpuTime = cpuTime;

            if (cpuDeltaMs < 0)
                return null;

            var normalized = (cpuDeltaMs / (elapsedMs * Environment.ProcessorCount)) * 100d;
            return Math.Round(Math.Max(0d, normalized), 2);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            || ex is NotSupportedException
            || ex is PlatformNotSupportedException)
        {
            _capabilities.CpuPercentSupported = false;
            return null;
        }
    }

    private int? TryReadThreadCount()
    {
        if (!_capabilities.ThreadCountSupported)
            return null;

        try
        {
            _process.Refresh();
            return _process.Threads.Count;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            || ex is NotSupportedException
            || ex is PlatformNotSupportedException)
        {
            _capabilities.ThreadCountSupported = false;
            return null;
        }
    }

    private static (double FrameTimeMs, string Quality) ResolveFrameEstimate()
    {
        const double fallbackRefreshRate = 60d;
        var refreshRate = TryReadDisplayRefreshRate();

        if (refreshRate.HasValue)
            return (1000d / refreshRate.Value, "estimated.display-refresh");

        return (1000d / fallbackRefreshRate, "estimated.default-60hz");
    }

    private static double? TryReadDisplayRefreshRate()
    {
        try
        {
            var refreshRate = DeviceDisplay.Current.MainDisplayInfo.RefreshRate;
            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 1d)
                return null;

            return refreshRate;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            || ex is NotSupportedException
            || ex is PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsAndroid()) return "Android";
        if (OperatingSystem.IsIOS()) return "iOS";
        if (OperatingSystem.IsMacCatalyst()) return "MacCatalyst";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        return "Unknown";
    }
}

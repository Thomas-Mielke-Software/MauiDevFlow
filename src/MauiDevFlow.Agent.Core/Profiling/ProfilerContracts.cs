namespace MauiDevFlow.Agent.Core.Profiling;

public class ProfilerSessionInfo
{
    public string SessionId { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public int SampleIntervalMs { get; set; }
    public bool IsActive { get; set; }
}

public class ProfilerSample
{
    public DateTime TsUtc { get; set; }
    public double? Fps { get; set; }
    public double? FrameTimeMsP50 { get; set; }
    public double? FrameTimeMsP95 { get; set; }
    public long ManagedBytes { get; set; }
    public int Gc0 { get; set; }
    public int Gc1 { get; set; }
    public int Gc2 { get; set; }
    public double? CpuPercent { get; set; }
    public int? ThreadCount { get; set; }
    public string FrameQuality { get; set; } = "estimated";
}

public class ProfilerMarker
{
    public DateTime TsUtc { get; set; }
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PayloadJson { get; set; }
}

public class ProfilerBatch
{
    public string SessionId { get; set; } = "";
    public List<ProfilerSample> Samples { get; set; } = new();
    public List<ProfilerMarker> Markers { get; set; } = new();
    public long SampleCursor { get; set; }
    public long MarkerCursor { get; set; }
    public bool IsActive { get; set; }
}

public class ProfilerCapabilities
{
    public bool SupportedInBuild { get; set; }
    public bool FeatureEnabled { get; set; }
    public string Platform { get; set; } = "unknown";
    public bool ManagedMemorySupported { get; set; }
    public bool GcSupported { get; set; }
    public bool CpuPercentSupported { get; set; }
    public bool FpsSupported { get; set; }
    public bool FrameTimingsEstimated { get; set; }
    public bool ThreadCountSupported { get; set; }
}

public class StartProfilerRequest
{
    public int? SampleIntervalMs { get; set; }
}

public class PublishProfilerMarkerRequest
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? PayloadJson { get; set; }
}

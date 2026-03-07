namespace MauiDevFlow.Agent.Core.Profiling;

public class ProfilerSessionStore
{
    private readonly ProfilerRingBuffer<ProfilerSample> _samples;
    private readonly ProfilerRingBuffer<ProfilerMarker> _markers;
    private readonly object _gate = new();
    private DateTime _lastSampleTimestampUtc = DateTime.MinValue;
    private DateTime _lastMarkerTimestampUtc = DateTime.MinValue;
    private ProfilerSessionInfo? _session;

    public ProfilerSessionStore(int maxSamples, int maxMarkers)
    {
        _samples = new ProfilerRingBuffer<ProfilerSample>(maxSamples);
        _markers = new ProfilerRingBuffer<ProfilerMarker>(maxMarkers);
    }

    public bool IsActive => _session?.IsActive == true;

    public ProfilerSessionInfo? CurrentSession
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    public ProfilerSessionInfo Start(int sampleIntervalMs)
    {
        lock (_gate)
        {
            _samples.Clear();
            _markers.Clear();
            _lastSampleTimestampUtc = DateTime.MinValue;
            _lastMarkerTimestampUtc = DateTime.MinValue;
            _session = new ProfilerSessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                StartedAtUtc = DateTime.UtcNow,
                SampleIntervalMs = sampleIntervalMs,
                IsActive = true
            };
            return _session;
        }
    }

    public ProfilerSessionInfo? Stop()
    {
        lock (_gate)
        {
            if (_session != null)
                _session.IsActive = false;
            return _session;
        }
    }

    public void AddSample(ProfilerSample sample)
    {
        lock (_gate)
        {
            if (_session?.IsActive != true)
                return;

            if (sample.TsUtc <= _lastSampleTimestampUtc)
                sample.TsUtc = _lastSampleTimestampUtc.AddTicks(1);
            _lastSampleTimestampUtc = sample.TsUtc;
            _samples.Add(sample);
        }
    }

    public void AddMarker(ProfilerMarker marker)
    {
        lock (_gate)
        {
            if (_session?.IsActive != true)
                return;

            if (marker.TsUtc <= _lastMarkerTimestampUtc)
                marker.TsUtc = _lastMarkerTimestampUtc.AddTicks(1);
            _lastMarkerTimestampUtc = marker.TsUtc;
            _markers.Add(marker);
        }
    }

    public ProfilerBatch GetBatch(long sampleCursor, long markerCursor, int limit)
    {
        lock (_gate)
        {
            if (_session == null)
            {
                return new ProfilerBatch
                {
                    SessionId = "",
                    IsActive = false,
                    Samples = new(),
                    Markers = new(),
                    SampleCursor = 0,
                    MarkerCursor = 0
                };
            }

            var samples = _samples.ReadAfter(sampleCursor, limit, out var latestSampleCursor);
            var markers = _markers.ReadAfter(markerCursor, limit, out var latestMarkerCursor);

            return new ProfilerBatch
            {
                SessionId = _session.SessionId,
                IsActive = _session.IsActive,
                Samples = samples,
                Markers = markers,
                SampleCursor = latestSampleCursor,
                MarkerCursor = latestMarkerCursor
            };
        }
    }
}

namespace MauiDevFlow.Agent.Core.Profiling;

public interface IMarkerPublisher
{
    void Publish(ProfilerMarker marker);
}

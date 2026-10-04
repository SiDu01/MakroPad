using System.Threading;
using System.Threading.Tasks;

namespace MakroPad.App.Services;

/// <summary>
/// Periodically samples system metrics in the background and exposes the latest values. Delta-based
/// metrics (CPU, network) need at least two samples before they report meaningful numbers.
/// </summary>
public interface ISystemMetricsSampler
{
    MetricsSnapshot Current { get; }

    /// <summary>Takes one synchronous sample and updates <see cref="Current"/>.</summary>
    void Sample();

    /// <summary>Runs a sampling loop until cancelled.</summary>
    Task RunSampleLoopAsync(CancellationToken cancellationToken);
}

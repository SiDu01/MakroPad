namespace MakroPad.Core.Device;

/// <summary>A line-based connection to a device (serial port or simulator).</summary>
public interface ILineTransport : IDisposable
{
    /// <summary>Human-readable endpoint, e.g. <c>COM5</c> or <c>Simulator</c>.</summary>
    string Name { get; }

    /// <summary>Raised for each received, non-empty line (any thread).</summary>
    event Action<string>? LineReceived;

    /// <summary>Raised once when the transport closes; carries the error if it failed.</summary>
    event Action<Exception?>? Closed;

    /// <summary>Starts delivering <see cref="LineReceived"/> events.</summary>
    void Start();

    void SendLine(string line);
}

using MakroPad.Core.Device;

namespace MakroPad.Core.Tests.Device;

internal sealed class FakeTransport : ILineTransport
{
    public List<string> Sent { get; } = [];

    public bool Disposed { get; private set; }

    public string Name => "Fake";

    public event Action<string>? LineReceived;

    public event Action<Exception?>? Closed;

    public void Start()
    {
    }

    public void SendLine(string line) => Sent.Add(line);

    public void Receive(string line) => LineReceived?.Invoke(line);

    public void Close(Exception? error = null) => Closed?.Invoke(error);

    public void Dispose() => Disposed = true;
}

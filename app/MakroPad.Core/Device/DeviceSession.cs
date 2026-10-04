using MakroPad.Core.Protocol;

namespace MakroPad.Core.Device;

public enum SessionState
{
    /// <summary>Transport open, waiting for the device's <c>hello</c>.</summary>
    Handshaking,
    Connected,
    /// <summary>No message from the device for <see cref="DeviceSession.UnresponsiveAfter"/>.</summary>
    Unresponsive,
    /// <summary>The device speaks a different protocol version.</summary>
    Incompatible,
    Closed
}

public enum LineDirection
{
    FromDevice,
    ToDevice
}

/// <summary>
/// Protocol v2 session on top of a transport: answers <c>hello</c> and <c>ping</c>, keeps the
/// heartbeat and turns <c>button</c>/<c>slider</c> lines into <see cref="InputEvent"/>s.
/// </summary>
public sealed class DeviceSession : IDisposable
{
    public static readonly TimeSpan PingAfter = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan UnresponsiveAfter = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

    private readonly ILineTransport transport;
    private readonly TimeProvider timeProvider;
    private readonly string appVersion;
    private readonly object gate = new();
    private ITimer? heartbeatTimer;
    private DateTimeOffset lastDeviceSeen;
    private DateTimeOffset lastPingSent = DateTimeOffset.MinValue;
    private int nextPingId;

    public DeviceSession(ILineTransport transport, TimeProvider timeProvider, string appVersion)
    {
        this.transport = transport;
        this.timeProvider = timeProvider;
        this.appVersion = appVersion;
    }

    public string Endpoint => transport.Name;

    public SessionState State { get; private set; } = SessionState.Handshaking;

    /// <summary>Device info from the last <c>hello</c>.</summary>
    public DeviceInfo? Device { get; private set; }

    public event Action<SessionState>? StateChanged;

    /// <summary>Raised after each handshake (also when the device rebooted); send the full state here.</summary>
    public event Action<DeviceInfo>? HandshakeCompleted;

    public event Action<InputEvent>? InputReceived;

    public event Action<LineDirection, string>? LineLogged;

    public void Start()
    {
        lastDeviceSeen = timeProvider.GetUtcNow();
        transport.LineReceived += OnLineReceived;
        transport.Closed += OnTransportClosed;
        heartbeatTimer = timeProvider.CreateTimer(_ => OnHeartbeatTick(), null, TickInterval, TickInterval);
        transport.Start();
    }

    /// <summary>Sends a line if the session is usable; returns false otherwise.</summary>
    public bool TrySend(string line)
    {
        if (State is not (SessionState.Connected or SessionState.Unresponsive))
        {
            return false;
        }

        return SendRaw(line);
    }

    public void Dispose()
    {
        heartbeatTimer?.Dispose();
        transport.LineReceived -= OnLineReceived;
        transport.Closed -= OnTransportClosed;
        transport.Dispose();
        SetState(SessionState.Closed);
    }

    private bool SendRaw(string line)
    {
        try
        {
            transport.SendLine(line);
            LineLogged?.Invoke(LineDirection.ToDevice, line);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            OnTransportClosed(ex);
            return false;
        }
    }

    private void OnLineReceived(string line)
    {
        LineLogged?.Invoke(LineDirection.FromDevice, line);

        ProtocolMessage message;
        try
        {
            message = ProtocolCodec.Parse(line);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return;
        }

        lock (gate)
        {
            lastDeviceSeen = timeProvider.GetUtcNow();
        }

        switch (message.Type.ToLowerInvariant())
        {
            case "hello":
                HandleHello(message);
                return;
            case "ping":
                SendRaw(DeviceMessages.Pong(message.GetValue("id")));
                break;
            case "button":
                if (TryGetInt(message, "id", out var buttonId) && message.GetValue("state") is "down" or "up")
                {
                    InputReceived?.Invoke(new ButtonInput(buttonId, message.GetValue("state") == "down"));
                }
                break;
            case "slider":
                if (TryGetInt(message, "id", out var sliderId) && TryGetInt(message, "value", out var value))
                {
                    InputReceived?.Invoke(new SliderInput(sliderId, Math.Clamp(value, 0, SliderInput.MaxValue)));
                }
                break;
        }

        if (State == SessionState.Unresponsive)
        {
            SetState(SessionState.Connected);
        }
    }

    private void HandleHello(ProtocolMessage message)
    {
        var info = DeviceInfo.FromHello(message);
        Device = info;

        if (info.ProtocolVersion != DeviceMessages.ProtocolVersion)
        {
            SetState(SessionState.Incompatible);
            return;
        }

        SetState(SessionState.Connected);
        SendRaw(DeviceMessages.HelloAck(appVersion));
        HandshakeCompleted?.Invoke(info);
    }

    private void OnHeartbeatTick()
    {
        if (State is not (SessionState.Connected or SessionState.Unresponsive))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        TimeSpan silence;
        bool shouldPing;
        lock (gate)
        {
            silence = now - lastDeviceSeen;
            shouldPing = silence >= PingAfter && now - lastPingSent >= PingAfter;
            if (shouldPing)
            {
                lastPingSent = now;
            }
        }

        if (silence >= UnresponsiveAfter)
        {
            SetState(SessionState.Unresponsive);
        }

        if (shouldPing)
        {
            SendRaw(DeviceMessages.Ping(Interlocked.Increment(ref nextPingId)));
        }
    }

    private void OnTransportClosed(Exception? error)
    {
        heartbeatTimer?.Dispose();
        SetState(SessionState.Closed);
    }

    private void SetState(SessionState state)
    {
        lock (gate)
        {
            if (State == state || State == SessionState.Closed)
            {
                return;
            }

            State = state;
        }

        StateChanged?.Invoke(state);
    }

    private static bool TryGetInt(ProtocolMessage message, string key, out int value)
        => int.TryParse(message.GetValue(key), out value);
}

public sealed record DeviceInfo(int ProtocolVersion, string Firmware, string Name, int Buttons, int Sliders)
{
    public static DeviceInfo FromHello(ProtocolMessage hello)
    {
        static int Int(ProtocolMessage m, string key) => int.TryParse(m.GetValue(key), out var v) ? v : 0;

        return new DeviceInfo(
            Int(hello, "proto"),
            hello.GetValue("fw") ?? "",
            hello.GetValue("device") ?? "",
            Int(hello, "buttons"),
            Int(hello, "sliders"));
    }
}

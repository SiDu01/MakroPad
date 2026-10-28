using MakroPad.Core.Device;
using Microsoft.Extensions.Time.Testing;

namespace MakroPad.Core.Tests.Device;

public sealed class DeviceSessionTests
{
    private const string Hello = "hello;proto=2;fw=0.3.0;device=makropad;buttons=16;sliders=3;display=ssd1306-128x64";

    private readonly FakeTransport transport = new();
    private readonly FakeTimeProvider time = new();
    private readonly DeviceSession session;

    public DeviceSessionTests()
    {
        session = new DeviceSession(transport, time, "1.2.3");
        session.Start();
    }

    [Fact]
    public void HelloIsAcknowledgedAndCompletesHandshake()
    {
        DeviceInfo? handshake = null;
        session.HandshakeCompleted += info => handshake = info;

        transport.Receive(Hello);

        Assert.Equal(SessionState.Connected, session.State);
        Assert.Equal("hello-ack;proto=2;app=1.2.3", Assert.Single(transport.Sent));
        Assert.NotNull(handshake);
        Assert.Equal(16, handshake.Buttons);
        Assert.Equal("0.3.0", handshake.Firmware);
    }

    [Fact]
    public void RepeatedHelloIsAcknowledgedAgain()
    {
        transport.Receive(Hello);
        transport.Receive(Hello);

        Assert.Equal(2, transport.Sent.Count(line => line.StartsWith("hello-ack", StringComparison.Ordinal)));
    }

    [Fact]
    public void WrongProtocolVersionIsIncompatible()
    {
        transport.Receive("hello;proto=1;fw=0.2.0;device=makropad-s3");

        Assert.Equal(SessionState.Incompatible, session.State);
        Assert.Empty(transport.Sent);
        Assert.False(session.TrySend("clear"));
    }

    [Fact]
    public void ButtonAndSliderLinesBecomeInputs()
    {
        var inputs = new List<InputEvent>();
        session.InputReceived += inputs.Add;
        transport.Receive(Hello);

        transport.Receive("button;id=3;state=down");
        transport.Receive("button;id=3;state=up");
        transport.Receive("slider;id=1;value=734");
        transport.Receive("button;id=x;state=down");
        transport.Receive("slider;id=2;value=5000");

        Assert.Equal(
            [new ButtonInput(3, true), new ButtonInput(3, false), new SliderInput(1, 734), new SliderInput(2, 1000)],
            inputs);
    }

    [Fact]
    public void PingIsAnswered()
    {
        transport.Receive(Hello);
        transport.Receive("ping;id=9");

        Assert.Equal("pong;id=9", transport.Sent.Last());
    }

    [Fact]
    public void SilenceTriggersPingThenUnresponsiveAndRecovers()
    {
        transport.Receive(Hello);

        time.Advance(TimeSpan.FromMilliseconds(2500));
        Assert.StartsWith("ping;id=", transport.Sent.Last(), StringComparison.Ordinal);
        Assert.Equal(SessionState.Connected, session.State);

        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(SessionState.Unresponsive, session.State);

        transport.Receive("pong;id=1");
        Assert.Equal(SessionState.Connected, session.State);
    }

    [Fact]
    public void BusyDeviceStillGetsPingedWhenAppIsQuiet()
    {
        transport.Receive(Hello);

        for (var i = 0; i < 6; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            transport.Receive("slider;id=1;value=500");
        }

        Assert.StartsWith("ping;id=", transport.Sent.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public void TransportCloseEndsSession()
    {
        transport.Receive(Hello);
        transport.Close(new IOException("unplugged"));

        Assert.Equal(SessionState.Closed, session.State);
        Assert.False(session.TrySend("clear"));
    }

    [Fact]
    public void SendIsRefusedBeforeHandshake()
    {
        Assert.False(session.TrySend("clear"));
        Assert.Empty(transport.Sent);
    }
}

using MakroPad.Core.Device;

namespace MakroPad.Core.Tests.Device;

public sealed class BluetoothSupportTests
{
    [Fact]
    public void AssemblerJoinsChunksAndKeepsTheUnfinishedTail()
    {
        var assembler = new LineAssembler();

        Assert.Empty(assembler.Feed("hello;pro"));
        Assert.Equal(["hello;proto=2", "ping"], assembler.Feed("to=2\r\n\nping\nbut"));
        Assert.Equal(["button;id=1"], assembler.Feed("ton;id=1\n"));
    }

    [Fact]
    public async Task FallbackReturnsTheFirstConnectorThatFindsADevice()
    {
        var bluetooth = new FakeTransport();
        var connector = new FallbackDeviceConnector(new StubConnector(null), new StubConnector(bluetooth), new StubConnector(new FakeTransport()));

        Assert.Same(bluetooth, await connector.ConnectAsync(CancellationToken.None));
        Assert.Null(await new FallbackDeviceConnector(new StubConnector(null)).ConnectAsync(CancellationToken.None));
    }

    private sealed class StubConnector(ILineTransport? transport) : IDeviceConnector
    {
        public Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken) => Task.FromResult(transport);
    }
}

using MakroPad.Actions;
using MakroPad.App.Services;
using MakroPad.Core.DisplayTriggers;
using MakroPad.Core.Protocol;
using MakroPad.Device;

namespace MakroPad.App.Tests.Services;

public sealed class DeviceDisplaySyncServiceTests
{
    [Fact]
    public async Task SendTemporaryTemplateAsyncSendsLineSizesOnlyWhenNeeded()
    {
        var connection = new RecordingConnection();
        var service = new DeviceDisplaySyncService(new FakeSystemVolumeController());

        await service.SendTemporaryTemplateAsync(
            connection,
            [
                new DisplayTemplateLine("Title", 2),
                new DisplayTemplateLine("Body", 1)
            ],
            1500);

        var message = ProtocolCodec.Parse(connection.SentLines.Single());
        Assert.Equal("display", message.Type);
        Assert.Equal("template", message.Values["mode"]);
        Assert.Equal("1500", message.Values["durationMs"]);
        Assert.Equal("Title", message.Values["line1"]);
        Assert.Equal("2", message.Values["line1Size"]);
        Assert.Equal("Body", message.Values["line2"]);
        Assert.False(message.Values.ContainsKey("line2Size"));
    }

    [Fact]
    public async Task SendTemporaryLayoutAsyncSendsLayoutFields()
    {
        var connection = new RecordingConnection();
        var service = new DeviceDisplaySyncService(new FakeSystemVolumeController());

        await service.SendTemporaryLayoutAsync(
            connection,
            new DisplayLayoutScreen("volume", "Volume", "System", ValuePercent: 72, Icon: "check"),
            1500);

        var message = ProtocolCodec.Parse(connection.SentLines.Single());
        Assert.Equal("display", message.Type);
        Assert.Equal("layout", message.Values["mode"]);
        Assert.Equal("volume", message.Values["layout"]);
        Assert.Equal("1500", message.Values["durationMs"]);
        Assert.Equal("Volume", message.Values["title"]);
        Assert.Equal("System", message.Values["line1"]);
        Assert.Equal("72", message.Values["value"]);
        Assert.Equal("check", message.Values["icon"]);
    }

    private sealed class RecordingConnection : IMakroPadConnection
    {
        public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Connected;

        public List<string> SentLines { get; } = [];

        public event EventHandler<DeviceEvent>? EventReceived;

        public event EventHandler<DeviceConnectionState>? StateChanged;

        public IReadOnlyList<string> GetAvailablePorts() => [];

        public Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
        {
            State = DeviceConnectionState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = DeviceConnectionState.Disconnected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task SendDisplayMessageAsync(string text, CancellationToken cancellationToken = default)
        {
            SentLines.Add($"display;mode=message;text={ProtocolCodec.PercentEncode(text)}");
            return Task.CompletedTask;
        }

        public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
        {
            SentLines.Add(line);
            return Task.CompletedTask;
        }

        public void RaiseEvent(DeviceEvent deviceEvent) => EventReceived?.Invoke(this, deviceEvent);
    }

    private sealed class FakeSystemVolumeController : ISystemVolumeController
    {
        public Task<int?> GetMasterVolumePercentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(50);

        public Task<ActionExecutionResult> SetMasterVolumeAsync(
            int percent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ActionExecutionResult.Ok("Volume updated."));
    }
}

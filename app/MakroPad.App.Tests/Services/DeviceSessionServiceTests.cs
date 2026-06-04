using MakroPad.App.Services;
using MakroPad.Device;

namespace MakroPad.App.Tests.Services;

public sealed class DeviceSessionServiceTests
{
    [Fact]
    public async Task SpontaneousConnectionDisconnectClearsActiveConnection()
    {
        var simulatedConnection = new SimulatedMakroPadConnection();
        var service = new DeviceSessionService(simulatedConnection);

        await service.ConnectAsync(DeviceSessionService.SimulatorPort);
        await simulatedConnection.DisconnectAsync();

        Assert.Null(service.ActiveConnection);
        Assert.Equal(DeviceConnectionState.Disconnected, service.State);
    }

    [Fact]
    public async Task ReconnectDoesNotPublishIntermediateDisconnectedState()
    {
        var service = new DeviceSessionService(new SimulatedMakroPadConnection());
        var states = new List<DeviceConnectionState>();
        service.StateChanged += (_, state) => states.Add(state);

        await service.ConnectAsync(DeviceSessionService.SimulatorPort);
        states.Clear();

        await service.ConnectAsync(DeviceSessionService.SimulatorPort);

        Assert.DoesNotContain(DeviceConnectionState.Disconnected, states);
        Assert.Equal(DeviceConnectionState.Connected, service.State);
    }
}

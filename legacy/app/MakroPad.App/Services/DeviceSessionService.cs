using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MakroPad.Device;

namespace MakroPad.App.Services;

public sealed class DeviceSessionService
{
    public const string SimulatorPort = "Simulator";

    private readonly SimulatedMakroPadConnection simulatedConnection;
    private readonly SerialMakroPadConnection serialConnection;
    private readonly MakroPadDeviceProbe deviceProbe;
    private readonly SemaphoreSlim sessionLock = new(1, 1);

    public DeviceSessionService(
        SimulatedMakroPadConnection? simulatedConnection = null,
        SerialMakroPadConnection? serialConnection = null,
        MakroPadDeviceProbe? deviceProbe = null)
    {
        this.simulatedConnection = simulatedConnection ?? new SimulatedMakroPadConnection();
        this.serialConnection = serialConnection ?? new SerialMakroPadConnection();
        this.deviceProbe = deviceProbe ?? new MakroPadDeviceProbe();
    }

    public IMakroPadConnection? ActiveConnection { get; private set; }

    public bool IsSimulatorActive => ReferenceEquals(ActiveConnection, simulatedConnection);

    public DeviceConnectionState State =>
        ActiveConnection?.State ?? DeviceConnectionState.Disconnected;

    public event EventHandler<DeviceEvent>? EventReceived;

    public event EventHandler<DeviceConnectionState>? StateChanged;

    public IReadOnlyList<string> GetAvailablePorts()
    {
        return
        [
            SimulatorPort,
            ..serialConnection.GetAvailablePorts()
        ];
    }

    public async Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectCurrentAsync(cancellationToken, publishDisconnected: false);
            ActiveConnection = portName.Equals(SimulatorPort, StringComparison.OrdinalIgnoreCase)
                ? simulatedConnection
                : serialConnection;
            ActiveConnection.EventReceived += OnEventReceived;
            ActiveConnection.StateChanged += OnStateChanged;

            try
            {
                await ActiveConnection.ConnectAsync(portName, cancellationToken);
            }
            catch
            {
                await DisconnectCurrentAsync(cancellationToken, publishDisconnected: true);
                throw;
            }
        }
        finally
        {
            sessionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectCurrentAsync(cancellationToken, publishDisconnected: true);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private async Task DisconnectCurrentAsync(
        CancellationToken cancellationToken,
        bool publishDisconnected)
    {
        if (ActiveConnection is null)
        {
            return;
        }

        var connection = ActiveConnection;
        connection.EventReceived -= OnEventReceived;
        connection.StateChanged -= OnStateChanged;
        try
        {
            await connection.DisconnectAsync(cancellationToken);
        }
        finally
        {
            ActiveConnection = null;
            if (publishDisconnected)
            {
                StateChanged?.Invoke(this, DeviceConnectionState.Disconnected);
            }
        }
    }

    public async Task<string?> DetectMakroPadPortAsync(
        string? preferredPort,
        CancellationToken cancellationToken = default)
    {
        return await deviceProbe.FindMakroPadPortAsync(
            serialConnection.GetAvailablePorts(),
            preferredPort,
            cancellationToken);
    }

    public Task SendDisplayMessageAsync(string text, CancellationToken cancellationToken = default)
    {
        return ActiveConnection?.SendDisplayMessageAsync(text, cancellationToken)
            ?? Task.FromException(new InvalidOperationException("No active device connection."));
    }

    public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        return ActiveConnection?.SendLineAsync(line, cancellationToken)
            ?? Task.FromException(new InvalidOperationException("No active device connection."));
    }

    public bool TrySimulateButtonPress(int id)
    {
        if (!IsSimulatorActive)
        {
            return false;
        }

        simulatedConnection.SimulateButtonPress(id);
        return true;
    }

    public bool TrySimulateSliderChange(int id, int value)
    {
        if (!IsSimulatorActive)
        {
            return false;
        }

        simulatedConnection.SimulateSliderChange(id, value);
        return true;
    }

    private void OnEventReceived(object? sender, DeviceEvent deviceEvent)
    {
        EventReceived?.Invoke(this, deviceEvent);
    }

    private void OnStateChanged(object? sender, DeviceConnectionState state)
    {
        if (state == DeviceConnectionState.Disconnected
            && sender is IMakroPadConnection connection
            && ReferenceEquals(ActiveConnection, connection))
        {
            connection.EventReceived -= OnEventReceived;
            connection.StateChanged -= OnStateChanged;
            ActiveConnection = null;
        }

        StateChanged?.Invoke(this, state);
    }
}

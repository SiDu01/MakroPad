using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MakroPad.Device;

namespace MakroPad.App.Services;

public sealed class DeviceDisplayTriggerSink : IDisplayTriggerSink
{
    private readonly DeviceDisplaySyncService displaySyncService;
    private readonly Func<IMakroPadConnection?> connectionProvider;

    public DeviceDisplayTriggerSink(
        DeviceDisplaySyncService displaySyncService,
        Func<IMakroPadConnection?> connectionProvider)
    {
        this.displaySyncService = displaySyncService
            ?? throw new ArgumentNullException(nameof(displaySyncService));
        this.connectionProvider = connectionProvider
            ?? throw new ArgumentNullException(nameof(connectionProvider));
    }

    public Task SendTemporaryTemplateAsync(
        IReadOnlyList<string> lines,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        var connection = connectionProvider();
        if (connection is null || connection.State != DeviceConnectionState.Connected)
        {
            throw new InvalidOperationException("No active device connection.");
        }

        return displaySyncService.SendTemporaryTemplateAsync(
            connection,
            lines,
            durationMs,
            cancellationToken);
    }
}

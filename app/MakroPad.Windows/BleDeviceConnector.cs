using System.Runtime.InteropServices;
using MakroPad.Core.Device;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace MakroPad.Windows;

/// <summary>
/// Finds a MakroPad among the Bluetooth devices paired in Windows. Pairing itself happens in the
/// Windows Bluetooth settings (passkey shown on the device's OLED).
/// </summary>
public sealed class BleDeviceConnector : IDeviceConnector
{
    public async Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken)
    {
        DeviceInformationCollection services;
        try
        {
            // Only paired devices expose their GATT services as device interfaces.
            var selector = GattDeviceService.GetDeviceSelectorFromUuid(BleLineTransport.ServiceUuid);
            services = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return null;  // no Bluetooth adapter or access denied
        }

        foreach (var info in services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await BleLineTransport.TryOpenAsync(info.Id, cancellationToken) is { } transport)
            {
                return transport;
            }
        }

        return null;
    }
}

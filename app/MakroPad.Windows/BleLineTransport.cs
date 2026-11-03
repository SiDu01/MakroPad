using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Channels;
using MakroPad.Core.Device;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MakroPad.Windows;

/// <summary>Bluetooth LE Nordic UART Service as an <see cref="ILineTransport"/>.</summary>
public sealed class BleLineTransport : ILineTransport
{
    public static readonly Guid ServiceUuid = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid RxUuid = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid TxUuid = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

    private readonly GattDeviceService service;
    private readonly GattCharacteristic rx;
    private readonly GattCharacteristic tx;
    private readonly Channel<string> outgoing = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource stopping = new();
    private readonly object readGate = new();
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly LineAssembler assembler = new();
    private Task? writeLoop;
    private volatile bool started;
    private volatile bool disposed;
    private int closedRaised;

    private BleLineTransport(GattDeviceService service, GattCharacteristic rx, GattCharacteristic tx)
    {
        this.service = service;
        this.rx = rx;
        this.tx = tx;
    }

    public string Name => "Bluetooth";

    public event Action<string>? LineReceived;

    public event Action<Exception?>? Closed;

    /// <summary>Opens the paired device's UART service; null when it is out of range or not a MakroPad.</summary>
    public static async Task<BleLineTransport?> TryOpenAsync(string serviceId, CancellationToken cancellationToken)
    {
        GattDeviceService? service = null;
        try
        {
            service = await GattDeviceService.FromIdAsync(serviceId).AsTask(cancellationToken);
            if (service is null)
            {
                return null;
            }

            service.Session.MaintainConnection = true;
            // Uncached: only succeeds when the device actually answers.
            var txResult = await service.GetCharacteristicsForUuidAsync(TxUuid, BluetoothCacheMode.Uncached).AsTask(cancellationToken);
            var rxResult = await service.GetCharacteristicsForUuidAsync(RxUuid, BluetoothCacheMode.Cached).AsTask(cancellationToken);
            if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0
                || rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
            {
                service.Dispose();
                return null;
            }

            var transport = new BleLineTransport(service, rxResult.Characteristics[0], txResult.Characteristics[0]);
            transport.tx.ValueChanged += transport.OnValueChanged;
            var subscribed = await transport.tx
                .WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify)
                .AsTask(cancellationToken);
            if (subscribed != GattCommunicationStatus.Success)
            {
                transport.Dispose();
                return null;
            }

            service.Session.SessionStatusChanged += transport.OnSessionStatusChanged;
            return transport;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ObjectDisposedException)
        {
            service?.Dispose();
            return null;
        }
    }

    public void Start()
    {
        started = true;
        writeLoop ??= Task.Run(WriteLoopAsync);
    }

    public void SendLine(string line) => outgoing.Writer.TryWrite(line + "\n");

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        stopping.Cancel();
        outgoing.Writer.TryComplete();
        tx.ValueChanged -= OnValueChanged;
        service.Session.SessionStatusChanged -= OnSessionStatusChanged;
        service.Dispose();
        RaiseClosed(null);
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        if (!started || disposed)
        {
            return;
        }

        IReadOnlyList<string> lines;
        lock (readGate)
        {
            // The decoder keeps UTF-8 sequences that were split across notifications.
            var bytes = args.CharacteristicValue.ToArray();
            var chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length)];
            decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
            lines = assembler.Feed(new string(chars));
        }

        foreach (var line in lines)
        {
            LineReceived?.Invoke(line);
        }
    }

    private void OnSessionStatusChanged(GattSession sender, GattSessionStatusChangedEventArgs args)
    {
        if (args.Status == GattSessionStatus.Closed && !disposed)
        {
            RaiseClosed(new IOException($"Bluetooth connection closed ({args.Error})."));
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var text in outgoing.Reader.ReadAllAsync(stopping.Token))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                var chunk = Math.Max(20, service.Session.MaxPduSize - 3);
                for (var offset = 0; offset < bytes.Length; offset += chunk)
                {
                    var buffer = bytes.AsBuffer(offset, Math.Min(chunk, bytes.Length - offset));
                    var result = await rx.WriteValueWithResultAsync(buffer, GattWriteOption.WriteWithResponse).AsTask(stopping.Token);
                    if (result.Status != GattCommunicationStatus.Success)
                    {
                        throw new IOException($"Bluetooth write failed ({result.Status}).");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or COMException or ObjectDisposedException)
        {
            if (!disposed)
            {
                RaiseClosed(ex);
            }
        }
    }

    private void RaiseClosed(Exception? error)
    {
        if (Interlocked.Exchange(ref closedRaised, 1) == 0)
        {
            Closed?.Invoke(error);
        }
    }
}

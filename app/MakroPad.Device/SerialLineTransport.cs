using System.IO.Ports;
using System.Text;
using MakroPad.Core.Device;

namespace MakroPad.Device;

/// <summary>USB CDC serial port as an <see cref="ILineTransport"/>.</summary>
public sealed class SerialLineTransport : ILineTransport
{
    public const int BaudRate = 115200;

    private readonly SerialPort port;
    private readonly object writeGate = new();
    private Thread? readThread;
    private volatile bool disposed;
    private int closedRaised;

    private SerialLineTransport(SerialPort port)
    {
        this.port = port;
    }

    public string Name => port.PortName;

    public event Action<string>? LineReceived;

    public event Action<Exception?>? Closed;

    public static SerialLineTransport Open(string portName)
    {
        var port = CreatePort(portName);
        port.Open();
        return new SerialLineTransport(port);
    }

    internal static SerialPort CreatePort(string portName) => new(portName, BaudRate)
    {
        Encoding = Encoding.UTF8,
        NewLine = "\n",
        ReadTimeout = 250,
        WriteTimeout = 1000,
        // Native USB CDC on the ESP32-S3 only transmits once DTR is asserted.
        DtrEnable = true,
        RtsEnable = true
    };

    public void Start()
    {
        readThread ??= new Thread(ReadLoop) { IsBackground = true, Name = $"MakroPad serial {Name}" };
        readThread.Start();
    }

    public void SendLine(string line)
    {
        lock (writeGate)
        {
            port.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            port.Close();
        }
        catch (IOException)
        {
        }

        port.Dispose();
        readThread?.Join(TimeSpan.FromSeconds(1));
        RaiseClosed(null);
    }

    private void ReadLoop()
    {
        while (!disposed)
        {
            try
            {
                var line = port.ReadLine().Trim();
                if (line.Length > 0)
                {
                    LineReceived?.Invoke(line);
                }
            }
            catch (TimeoutException)
            {
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ObjectDisposedException)
            {
                if (!disposed)
                {
                    RaiseClosed(ex);
                }

                return;
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

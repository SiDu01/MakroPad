using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace MakroPad.App.Services;

/// <summary>
/// Samples system metrics. CPU (via GetSystemTimes) and network throughput are delta-based and read
/// 0 until the second sample; memory and battery are instantaneous. Windows-only signals are skipped
/// on other platforms (memory 0, battery 100); network sampling is cross-platform.
/// </summary>
public sealed class SystemMetricsSampler : ISystemMetricsSampler
{
    private volatile MetricsSnapshot current = MetricsSnapshot.Default;

    private ulong previousIdleTime;
    private ulong previousTotalTime;
    private bool hasCpuBaseline;
    private double lastCpuPercent;

    private long previousNetworkBytes;
    private DateTime previousNetworkAt;
    private bool hasNetworkBaseline;
    private double lastNetworkKbps;

    public MetricsSnapshot Current => current;

    public void Sample()
    {
        current = new MetricsSnapshot
        {
            CpuPercent = SampleCpu(),
            MemoryPercent = SampleMemory(),
            BatteryPercent = SampleBattery(),
            NetworkKbps = SampleNetwork()
        };
    }

    public async Task RunSampleLoopAsync(CancellationToken cancellationToken)
    {
        Sample(); // establish baselines for delta metrics
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                Sample();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Keep the loop alive; a single failed sample should not stop monitoring.
            }
        }
    }

    private double SampleCpu()
    {
        if (!OperatingSystem.IsWindows()
            || !GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return lastCpuPercent;
        }

        var idleTime = ToUInt64(idle);
        var totalTime = ToUInt64(kernel) + ToUInt64(user); // kernel time already includes idle

        if (hasCpuBaseline)
        {
            var idleDelta = idleTime - previousIdleTime;
            var totalDelta = totalTime - previousTotalTime;
            if (totalDelta > 0)
            {
                lastCpuPercent = Math.Clamp((1.0 - ((double)idleDelta / totalDelta)) * 100.0, 0, 100);
            }
        }

        previousIdleTime = idleTime;
        previousTotalTime = totalTime;
        hasCpuBaseline = true;
        return lastCpuPercent;
    }

    private static double SampleMemory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? status.MemoryLoad : 0;
    }

    private static double SampleBattery()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemPowerStatus(out var status))
        {
            return 100;
        }

        // 255 = unknown, and 128 in BatteryFlag means "no system battery" (desktop) — treat as full.
        return status.BatteryLifePercent is 255 || (status.BatteryFlag & 128) != 0
            ? 100
            : status.BatteryLifePercent;
    }

    private double SampleNetwork()
    {
        long totalBytes;
        try
        {
            totalBytes = SumNetworkBytes();
        }
        catch
        {
            return lastNetworkKbps;
        }

        var now = DateTime.UtcNow;
        if (hasNetworkBaseline)
        {
            var elapsedSeconds = (now - previousNetworkAt).TotalSeconds;
            if (elapsedSeconds > 0 && totalBytes >= previousNetworkBytes)
            {
                lastNetworkKbps = (totalBytes - previousNetworkBytes) / elapsedSeconds * 8.0 / 1000.0;
            }
        }

        previousNetworkBytes = totalBytes;
        previousNetworkAt = now;
        hasNetworkBaseline = true;
        return lastNetworkKbps;
    }

    private static long SumNetworkBytes()
    {
        long total = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            try
            {
                var stats = nic.GetIPStatistics();
                total += stats.BytesReceived + stats.BytesSent;
            }
            catch
            {
                // Some interfaces do not expose statistics — skip them.
            }
        }

        return total;
    }

    private static ulong ToUInt64(FILETIME time) =>
        ((ulong)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}

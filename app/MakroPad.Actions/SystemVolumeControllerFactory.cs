using System.Runtime.InteropServices;

namespace MakroPad.Actions;

public static class SystemVolumeControllerFactory
{
    public static ISystemVolumeController Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSystemVolumeController();
        }

        return new NoopSystemVolumeController();
    }
}

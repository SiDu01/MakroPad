using System.Runtime.InteropServices;

namespace MakroPad.Actions;

public static class ApplicationVolumeControllerFactory
{
    public static IApplicationVolumeController Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsApplicationVolumeController();
        }

        return new NoopApplicationVolumeController();
    }
}

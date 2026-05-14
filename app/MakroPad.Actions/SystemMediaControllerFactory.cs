using System.Runtime.InteropServices;

namespace MakroPad.Actions;

public static class SystemMediaControllerFactory
{
    public static ISystemMediaController Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSystemMediaController();
        }

        return new NoopSystemMediaController();
    }
}

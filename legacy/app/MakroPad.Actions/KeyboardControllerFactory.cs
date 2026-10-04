using System.Runtime.InteropServices;

namespace MakroPad.Actions;

public static class KeyboardControllerFactory
{
    public static IKeyboardController Create()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsKeyboardController()
            : new NoopKeyboardController();
    }
}

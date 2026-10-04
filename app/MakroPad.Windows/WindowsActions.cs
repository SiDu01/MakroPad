using MakroPad.Core.Actions;

namespace MakroPad.Windows;

public static class WindowsActions
{
    public static IEnumerable<IActionHandler> CreateAll()
    {
        foreach (var mediaKey in MediaKeyAction.All)
        {
            yield return mediaKey;
        }

        yield return new SystemVolumeAction();
        yield return new AppVolumeAction();
        yield return new ForegroundVolumeAction();
        yield return new OtherAppsVolumeAction();
        yield return new KeyboardShortcutAction();
        yield return new OpenUrlAction();
        yield return new OpenAppAction();
    }
}

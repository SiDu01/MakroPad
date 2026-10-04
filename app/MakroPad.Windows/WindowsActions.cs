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
    }
}

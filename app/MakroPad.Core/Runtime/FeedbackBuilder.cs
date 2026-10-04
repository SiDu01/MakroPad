using MakroPad.Core.Actions;
using MakroPad.Core.Protocol;

namespace MakroPad.Core.Runtime;

/// <summary>Turns action results into OLED overlays.</summary>
public static class FeedbackBuilder
{
    public const int VolumeOverlayMs = 1200;

    public static ScreenRequest ForResult(Binding binding, ActionResult result)
    {
        if (!result.Success)
        {
            return new ScreenRequest(ScreenType.Action, "Fehler", result.Message, Icon: ScreenIcon.Fail, DurationMs: 2500);
        }

        if (result.Percent is { } percent)
        {
            return new ScreenRequest(ScreenType.Volume, Text: result.Message, Value: percent, DurationMs: VolumeOverlayMs);
        }

        return new ScreenRequest(ScreenType.Action, Text: result.Message, Icon: ScreenIcon.Ok);
    }
}

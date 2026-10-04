using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
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

        var text = binding.GetParameter("label") is { Length: > 0 } label ? label : result.Message;
        if (result.Percent is { } percent)
        {
            return new ScreenRequest(ScreenType.Volume, Text: text, Value: percent, DurationMs: VolumeOverlayMs);
        }

        return new ScreenRequest(ScreenType.Action, Text: text, Icon: ScreenIcon.Ok);
    }

    public static ScreenRequest ForProfile(string name) => new(ScreenType.Profile, Text: name);

    /// <summary>Whether the user enabled an overlay for this result.</summary>
    public static bool IsEnabled(FeedbackSettings settings, ActionResult result)
    {
        if (result.Quiet)
        {
            return false;
        }

        if (!result.Success)
        {
            return settings.Errors;
        }

        return result.Percent is null ? settings.Actions : settings.Volume;
    }
}

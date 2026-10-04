using MakroPad.Core.Protocol;

namespace MakroPad.Core.Input;

public sealed class ProtocolInputDecoder
{
    public bool TryDecode(ProtocolMessage message, out MakroPadInputEvent? inputEvent)
    {
        ArgumentNullException.ThrowIfNull(message);

        inputEvent = null;
        if (message.Type.Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            if (message.GetValue("state")?.Equals("up", StringComparison.OrdinalIgnoreCase) != true
                || !TryParsePositiveId(message.GetValue("id"), out var buttonId))
            {
                return false;
            }

            inputEvent = new ButtonReleasedInputEvent(buttonId);
            return true;
        }

        if (message.Type.Equals("slider", StringComparison.OrdinalIgnoreCase)
            && TryParsePositiveId(message.GetValue("id"), out var sliderId)
            && int.TryParse(message.GetValue("value"), out var value))
        {
            inputEvent = new SliderChangedInputEvent(sliderId, value);
            return true;
        }

        return false;
    }

    private static bool TryParsePositiveId(string? value, out int id)
    {
        return int.TryParse(value, out id) && id > 0;
    }
}

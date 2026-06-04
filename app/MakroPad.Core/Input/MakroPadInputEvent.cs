namespace MakroPad.Core.Input;

public abstract record MakroPadInputEvent(string Input);

public sealed record ButtonReleasedInputEvent(int ButtonId)
    : MakroPadInputEvent($"button:{ButtonId}");

public sealed record SliderChangedInputEvent(int SliderId, int Value)
    : MakroPadInputEvent($"slider:{SliderId}");

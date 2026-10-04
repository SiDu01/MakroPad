namespace MakroPad.Core.Device;

/// <summary>A physical input change reported by the device.</summary>
public abstract record InputEvent
{
    /// <summary>Binding key, e.g. <c>button:3</c> or <c>slider:1</c>.</summary>
    public abstract string InputId { get; }
}

public sealed record ButtonInput(int Id, bool Pressed) : InputEvent
{
    public override string InputId => $"button:{Id}";
}

/// <summary>Slider position, normalized by the firmware to 0..1000.</summary>
public sealed record SliderInput(int Id, int Value) : InputEvent
{
    public const int MaxValue = 1000;

    public override string InputId => $"slider:{Id}";

    public int Percent => (int)Math.Round(Math.Clamp(Value, 0, MaxValue) / 10.0, MidpointRounding.AwayFromZero);
}

using MakroPad.Core.Actions;
using MakroPad.Core.Device;

namespace MakroPad.Core.Runtime;

/// <summary>
/// Decides which inputs trigger which binding. Buttons fire on press. Slider values pass only
/// when they moved at least <see cref="SliderDeadbandPercent"/> since the last executed value
/// (0 % and 100 % always pass) to keep audio APIs from being flooded with jitter.
/// </summary>
public sealed class InputRouter
{
    private readonly Dictionary<string, int> lastSliderPercent = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private IReadOnlyList<Binding> bindings;

    public InputRouter(IReadOnlyList<Binding> bindings)
    {
        this.bindings = bindings;
    }

    public int SliderDeadbandPercent { get; set; } = 2;

    public void SetBindings(IReadOnlyList<Binding> value)
    {
        lock (gate)
        {
            bindings = value;
            lastSliderPercent.Clear();
        }
    }

    /// <summary>Which gestures besides a normal press are bound for a key.</summary>
    public (bool Long, bool Double) GesturesFor(int buttonId)
    {
        lock (gate)
        {
            var prefix = $"button:{buttonId}";
            return (
                bindings.Any(b => string.Equals(b.Input, prefix + GestureInput.LongSuffix, StringComparison.OrdinalIgnoreCase)),
                bindings.Any(b => string.Equals(b.Input, prefix + GestureInput.DoubleSuffix, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Binding? Route(InputEvent input)
    {
        lock (gate)
        {
            return RouteLocked(input);
        }
    }

    private Binding? RouteLocked(InputEvent input)
    {
        if (input is ButtonInput { Pressed: false })
        {
            return null;
        }

        var binding = bindings.FirstOrDefault(b => string.Equals(b.Input, input.InputId, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
        {
            return null;
        }

        if (input is SliderInput slider && !PassesDeadband(slider))
        {
            return null;
        }

        return binding;
    }

    private bool PassesDeadband(SliderInput slider)
    {
        var percent = slider.Percent;
        if (lastSliderPercent.TryGetValue(slider.InputId, out var last)
            && percent is not 0 and not 100
            && Math.Abs(percent - last) < SliderDeadbandPercent)
        {
            return false;
        }

        if (lastSliderPercent.TryGetValue(slider.InputId, out last) && last == percent)
        {
            return false;
        }

        lastSliderPercent[slider.InputId] = percent;
        return true;
    }
}

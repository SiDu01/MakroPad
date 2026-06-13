using CommunityToolkit.Mvvm.ComponentModel;

namespace MakroPad.App.ViewModels;

/// <summary>
/// Base view model for a single addressable slot on the device (a button key or a slider),
/// shown as a tile/strip in the Pad grid.
/// </summary>
public abstract partial class PadSlotViewModel : ObservableObject
{
    protected PadSlotViewModel(string input, string label)
    {
        Input = input;
        Label = label;
    }

    /// <summary>Protocol input id, e.g. "button:7" or "slider:1".</summary>
    public string Input { get; }

    /// <summary>Human readable slot name, e.g. "Taste 7".</summary>
    public string Label { get; }

    [ObservableProperty]
    private string actionLabel = "Leer";

    [ObservableProperty]
    private bool hasBinding;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isActive;
}

public sealed class KeyViewModel : PadSlotViewModel
{
    public KeyViewModel(int number)
        : base($"button:{number}", $"Taste {number}")
    {
        Number = number;
    }

    public int Number { get; }
}

public sealed class SliderViewModel : PadSlotViewModel
{
    public SliderViewModel(int number)
        : base($"slider:{number}", $"Slider {number}")
    {
        Number = number;
    }

    public int Number { get; }
}

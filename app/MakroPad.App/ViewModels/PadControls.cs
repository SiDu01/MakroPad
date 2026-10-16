using CommunityToolkit.Mvvm.ComponentModel;

namespace MakroPad.App.ViewModels;

public static class PadGeometry
{
    public const double KeySize = 112;
    public const double KeyPitch = 124;

    /// <summary>Selectable parts are drawn 6 units larger on each side to make room for the selection ring.</summary>
    public const double RingInset = 6;
}

/// <summary>Something on the pad that can be selected in the editor.</summary>
public abstract partial class PadElementViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Lit while the physical input is active.</summary>
    [ObservableProperty]
    private bool isPressed;

    [ObservableProperty]
    private string legend = "";

    public abstract string Title { get; }

    public bool IsBound => Legend.Length > 0;

    /// <summary>Long single words get a smaller size so they do not break mid-word on the cap.</summary>
    public double LegendFontSize => Legend.Split(' ', '-', '/', '+').Max(word => word.Length) > 10 ? 13 : 15.5;

    partial void OnLegendChanged(string value)
    {
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(LegendFontSize));
    }
}

public sealed partial class KeyViewModel(int id, int row, int column) : PadElementViewModel
{
    /// <summary>"lang", "doppelt" or both when those gestures are bound; empty otherwise.</summary>
    [ObservableProperty]
    private string gestureHint = "";

    public int Id { get; } = id;

    public string Input => $"button:{Id}";

    public int Row { get; } = row;

    public int Column { get; } = column;

    /// <summary>Position inside the key area, which starts one ring inset left of and above key 1.</summary>
    public double X => Column * PadGeometry.KeyPitch;

    public double Y => Row * PadGeometry.KeyPitch;

    public override string Title => $"Taste {Id}";
}

public sealed partial class FaderViewModel(int id) : PadElementViewModel
{
    public int Id { get; } = id;

    public string Input => $"slider:{Id}";

    public override string Title => $"Slider {Id}";

    /// <summary>Last reported position, 0..1000.</summary>
    [ObservableProperty]
    private int value;

    public int Percent => (int)Math.Round(Value / 10.0, MidpointRounding.AwayFromZero);

    partial void OnValueChanged(int value) => OnPropertyChanged(nameof(Percent));
}

/// <summary>The OLED; selecting it edits which overlays appear.</summary>
public sealed class DisplayElementViewModel : PadElementViewModel
{
    public override string Title => "Display";
}

public sealed partial class ProfileTabViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private bool isActive;
}

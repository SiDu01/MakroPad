using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MakroPad.App.Controls;

/// <summary>A slide-potentiometer as seen from above: the slot in the lid and the cap at the live position.</summary>
public sealed class FaderView : Control
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<FaderView, int>(nameof(Value));

    public static readonly StyledProperty<IBrush?> SlotBrushProperty =
        AvaloniaProperty.Register<FaderView, IBrush?>(nameof(SlotBrush), Brushes.DarkSlateGray);

    public static readonly StyledProperty<IBrush?> CapBrushProperty =
        AvaloniaProperty.Register<FaderView, IBrush?>(nameof(CapBrush), Brushes.OrangeRed);

    public static readonly StyledProperty<IBrush?> CapShadeBrushProperty =
        AvaloniaProperty.Register<FaderView, IBrush?>(nameof(CapShadeBrush), Brushes.SaddleBrown);

    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<FaderView, double>(nameof(CapHeight), 64);

    static FaderView()
    {
        AffectsRender<FaderView>(ValueProperty, SlotBrushProperty, CapBrushProperty, CapShadeBrushProperty, CapHeightProperty);
    }

    /// <summary>0..1000; 1000 is the far end (top on the pad).</summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? SlotBrush
    {
        get => GetValue(SlotBrushProperty);
        set => SetValue(SlotBrushProperty, value);
    }

    public IBrush? CapBrush
    {
        get => GetValue(CapBrushProperty);
        set => SetValue(CapBrushProperty, value);
    }

    public IBrush? CapShadeBrush
    {
        get => GetValue(CapShadeBrushProperty);
        set => SetValue(CapShadeBrushProperty, value);
    }

    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var slotWidth = Math.Max(6, width * 0.16);
        var capHeight = Math.Min(CapHeight, height / 3);
        var travel = height - capHeight;

        context.DrawRectangle(SlotBrush, null, new RoundedRect(new Rect((width - slotWidth) / 2, 0, slotWidth, height), slotWidth / 2));

        var top = travel * (1 - (Math.Clamp(Value, 0, 1000) / 1000.0));
        var shade = Math.Max(4, capHeight * 0.12);
        context.DrawRectangle(CapShadeBrush, null, new RoundedRect(new Rect(0, top + shade, width, capHeight - shade), 3));
        context.DrawRectangle(CapBrush, null, new RoundedRect(new Rect(0, top, width, capHeight - shade), 3));
    }
}

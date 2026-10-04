using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MakroPad.Core.Display;

namespace MakroPad.App.Controls;

/// <summary>
/// Draws the <see cref="OledMirror"/> as a 128x64 dot matrix: each lit pixel is a small square with a
/// hairline gap, so the preview reads as the real panel rather than a scaled screenshot.
/// </summary>
public sealed class OledView : Control
{
    public static readonly StyledProperty<OledMirror?> MirrorProperty =
        AvaloniaProperty.Register<OledView, OledMirror?>(nameof(Mirror));

    public static readonly StyledProperty<IBrush?> PixelBrushProperty =
        AvaloniaProperty.Register<OledView, IBrush?>(nameof(PixelBrush), Brushes.White);

    public static readonly StyledProperty<IBrush?> GlassBrushProperty =
        AvaloniaProperty.Register<OledView, IBrush?>(nameof(GlassBrush), Brushes.Black);

    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly bool[] shown = new bool[OledCanvas.Width * OledCanvas.Height];

    static OledView()
    {
        AffectsRender<OledView>(PixelBrushProperty, GlassBrushProperty);
    }

    public OledView()
    {
        timer.Tick += (_, _) => Poll();
    }

    public OledMirror? Mirror
    {
        get => GetValue(MirrorProperty);
        set => SetValue(MirrorProperty, value);
    }

    public IBrush? PixelBrush
    {
        get => GetValue(PixelBrushProperty);
        set => SetValue(PixelBrushProperty, value);
    }

    public IBrush? GlassBrush
    {
        get => GetValue(GlassBrushProperty);
        set => SetValue(GlassBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Keep the panel's 2:1 aspect ratio.
        var width = double.IsInfinity(availableSize.Width) ? 256 : availableSize.Width;
        var height = Math.Min(width / 2, double.IsInfinity(availableSize.Height) ? width / 2 : availableSize.Height);
        return new Size(height * 2, height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        timer.Start();
        Poll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(GlassBrush ?? Brushes.Black, bounds);

        var scale = Math.Min(bounds.Width / OledCanvas.Width, bounds.Height / OledCanvas.Height);
        var offsetX = (bounds.Width - (scale * OledCanvas.Width)) / 2;
        var offsetY = (bounds.Height - (scale * OledCanvas.Height)) / 2;
        var dot = Math.Max(0.6, scale * 0.9);
        var brush = PixelBrush ?? Brushes.White;

        for (var y = 0; y < OledCanvas.Height; y++)
        {
            for (var x = 0; x < OledCanvas.Width; x++)
            {
                if (shown[(y * OledCanvas.Width) + x])
                {
                    context.FillRectangle(brush, new Rect(offsetX + (x * scale), offsetY + (y * scale), dot, dot));
                }
            }
        }
    }

    private void Poll()
    {
        if (Mirror is null)
        {
            return;
        }

        var frame = Mirror.Render().Pixels;
        if (!frame.SequenceEqual(shown))
        {
            frame.CopyTo(shown);
            InvalidateVisual();
        }
    }
}

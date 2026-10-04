namespace MakroPad.Core.Display;

/// <summary>
/// 128x64 monochrome frame buffer with the Adafruit GFX primitives the firmware uses
/// (same algorithms, so lines, circles and text land on the same pixels).
/// </summary>
public sealed class OledCanvas
{
    public const int Width = 128;
    public const int Height = 64;

    private readonly bool[] pixels = new bool[Width * Height];

    public bool this[int x, int y] => x is >= 0 and < Width && y is >= 0 and < Height && pixels[(y * Width) + x];

    public ReadOnlySpan<bool> Pixels => pixels;

    public void Clear() => Array.Clear(pixels);

    public void SetPixel(int x, int y, bool on)
    {
        if (x is >= 0 and < Width && y is >= 0 and < Height)
        {
            pixels[(y * Width) + x] = on;
        }
    }

    public void FillRect(int x, int y, int w, int h, bool on)
    {
        for (var row = y; row < y + h; row++)
        {
            for (var column = x; column < x + w; column++)
            {
                SetPixel(column, row, on);
            }
        }
    }

    public void DrawRect(int x, int y, int w, int h, bool on)
    {
        FillRect(x, y, w, 1, on);
        FillRect(x, y + h - 1, w, 1, on);
        FillRect(x, y, 1, h, on);
        FillRect(x + w - 1, y, 1, h, on);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, bool on)
    {
        var steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
        if (steep)
        {
            (x0, y0) = (y0, x0);
            (x1, y1) = (y1, x1);
        }

        if (x0 > x1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        var dx = x1 - x0;
        var dy = Math.Abs(y1 - y0);
        var err = dx / 2;
        var yStep = y0 < y1 ? 1 : -1;
        for (; x0 <= x1; x0++)
        {
            if (steep)
            {
                SetPixel(y0, x0, on);
            }
            else
            {
                SetPixel(x0, y0, on);
            }

            err -= dy;
            if (err < 0)
            {
                y0 += yStep;
                err += dx;
            }
        }
    }

    public void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2, bool on)
    {
        DrawLine(x0, y0, x1, y1, on);
        DrawLine(x1, y1, x2, y2, on);
        DrawLine(x2, y2, x0, y0, on);
    }

    public void DrawCircle(int x0, int y0, int r, bool on)
    {
        var f = 1 - r;
        var ddFx = 1;
        var ddFy = -2 * r;
        var x = 0;
        var y = r;
        SetPixel(x0, y0 + r, on);
        SetPixel(x0, y0 - r, on);
        SetPixel(x0 + r, y0, on);
        SetPixel(x0 - r, y0, on);
        while (x < y)
        {
            if (f >= 0)
            {
                y--;
                ddFy += 2;
                f += ddFy;
            }

            x++;
            ddFx += 2;
            f += ddFx;
            SetPixel(x0 + x, y0 + y, on);
            SetPixel(x0 - x, y0 + y, on);
            SetPixel(x0 + x, y0 - y, on);
            SetPixel(x0 - x, y0 - y, on);
            SetPixel(x0 + y, y0 + x, on);
            SetPixel(x0 - y, y0 + x, on);
            SetPixel(x0 + y, y0 - x, on);
            SetPixel(x0 - y, y0 - x, on);
        }
    }

    /// <summary>Prints CP437 bytes like Adafruit_GFX::print with text wrap enabled and a transparent background.</summary>
    public void Print(int x, int y, ReadOnlySpan<byte> text, int size, bool on)
    {
        var cursorX = x;
        var cursorY = y;
        foreach (var c in text)
        {
            if (c == (byte)'\n')
            {
                cursorX = 0;
                cursorY += 8 * size;
                continue;
            }

            if (c == (byte)'\r')
            {
                continue;
            }

            if (cursorX + (6 * size) > Width)
            {
                cursorX = 0;
                cursorY += 8 * size;
            }

            DrawChar(cursorX, cursorY, c, size, on);
            cursorX += 6 * size;
        }
    }

    private void DrawChar(int x, int y, byte c, int size, bool on)
    {
        var font = GlcdFont.Data;
        for (var column = 0; column < 5; column++)
        {
            var line = font[(c * 5) + column];
            for (var row = 0; row < 8; row++, line >>= 1)
            {
                if ((line & 1) != 0)
                {
                    FillRect(x + (column * size), y + (row * size), size, size, on);
                }
            }
        }
    }
}

using System.Text;

namespace Snake.Ui;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Lerp(Rgb from, Rgb to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new Rgb(
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }
}

public readonly record struct AnsiStyle(
    Rgb? Foreground = null,
    Rgb? Background = null,
    bool Bold = false,
    bool Dim = false)
{
    public static readonly AnsiStyle Default = new();

    public AnsiStyle AsDim() => this with { Dim = true };
}

/// <summary>
/// A small, dependency-free cell canvas. Every cell is one terminal column,
/// which keeps layout math predictable for the fixed-size arcade panel.
/// </summary>
public sealed class Canvas
{
    private readonly CellStyle[,] _cells;
    private readonly bool _useColor;

    public Canvas(int width, int height, bool useColor)
    {
        Width = width;
        Height = height;
        _useColor = useColor;
        _cells = new CellStyle[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _cells[x, y] = new CellStyle(' ', AnsiStyle.Default);
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    public void Set(int x, int y, char character, AnsiStyle style)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        _cells[x, y] = new CellStyle(character, style);
    }

    public void Write(int x, int y, string text, AnsiStyle style)
    {
        for (int i = 0; i < text.Length; i++)
        {
            Set(x + i, y, text[i], style);
        }
    }

    public void WriteCentered(int y, string text, AnsiStyle style) =>
        Write((Width - text.Length) / 2, y, text, style);

    public void FillRect(int x, int y, int width, int height, char character, AnsiStyle style)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                Set(column, row, character, style);
            }
        }
    }

    public void DrawBorder(int x, int y, int width, int height, AnsiStyle style)
    {
        Set(x, y, '╭', style);
        Set(x + width - 1, y, '╮', style);
        Set(x, y + height - 1, '╰', style);
        Set(x + width - 1, y + height - 1, '╯', style);

        for (int column = x + 1; column < x + width - 1; column++)
        {
            Set(column, y, '─', style);
            Set(column, y + height - 1, '─', style);
        }

        for (int row = y + 1; row < y + height - 1; row++)
        {
            Set(x, row, '│', style);
            Set(x + width - 1, row, '│', style);
        }
    }

    public void DimRegion(int x, int y, int width, int height)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                if (column < 0 || column >= Width || row < 0 || row >= Height)
                {
                    continue;
                }

                CellStyle cell = _cells[column, row];
                _cells[column, row] = new CellStyle(cell.Character, cell.Style.AsDim());
            }
        }
    }

    public string[] RenderLines()
    {
        var lines = new string[Height];

        for (int y = 0; y < Height; y++)
        {
            var builder = new StringBuilder(Width + 32);
            AnsiStyle activeStyle = AnsiStyle.Default;

            for (int x = 0; x < Width; x++)
            {
                CellStyle cell = _cells[x, y];
                if (_useColor && cell.Style != activeStyle)
                {
                    builder.Append(StyleEscape(cell.Style));
                    activeStyle = cell.Style;
                }

                builder.Append(cell.Character);
            }

            if (_useColor && activeStyle != AnsiStyle.Default)
            {
                builder.Append("\x1b[0m");
            }

            lines[y] = builder.ToString();
        }

        return lines;
    }

    private static string StyleEscape(AnsiStyle style)
    {
        var builder = new StringBuilder(32);
        builder.Append("\x1b[0m");

        if (style.Foreground is { } foreground)
        {
            builder.Append("\x1b[38;2;")
                .Append(foreground.R).Append(';')
                .Append(foreground.G).Append(';')
                .Append(foreground.B).Append('m');
        }

        if (style.Background is { } background)
        {
            builder.Append("\x1b[48;2;")
                .Append(background.R).Append(';')
                .Append(background.G).Append(';')
                .Append(background.B).Append('m');
        }

        if (style.Bold)
        {
            builder.Append("\x1b[1m");
        }

        if (style.Dim)
        {
            builder.Append("\x1b[2m");
        }

        return builder.ToString();
    }

    private readonly record struct CellStyle(char Character, AnsiStyle Style);
}

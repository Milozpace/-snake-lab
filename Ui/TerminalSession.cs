using System.Text;

namespace Snake.Ui;

/// <summary>
/// Owns the alternate screen buffer and draws the fixed-size game panel,
/// centered inside the current terminal. Everything is restored on dispose.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    public const int MinimumWidth = GameRenderer.CanvasWidth + 2;
    public const int MinimumHeight = GameRenderer.CanvasHeight;

    private readonly bool _useColor;
    private bool _active;
    private int _lastWidth;
    private int _lastHeight;

    public TerminalSession(bool useColor)
    {
        _useColor = useColor;
    }

    public (int Width, int Height) GetDimensions()
    {
        try
        {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            return (width > 0 ? width : 80, height > 0 ? height : 24);
        }
        catch (IOException)
        {
            return (80, 24);
        }
        catch (PlatformNotSupportedException)
        {
            return (80, 24);
        }
    }

    public bool IsLargeEnough()
    {
        var (width, height) = GetDimensions();
        return width >= MinimumWidth && height >= MinimumHeight;
    }

    public void Enter()
    {
        if (_active)
        {
            return;
        }

        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        Console.Write("\x1b[?1049h\x1b[?7l\x1b[?25l\x1b[2J\x1b[H");
        _active = true;
        (_lastWidth, _lastHeight) = GetDimensions();
    }

    public void Render(Canvas canvas)
    {
        if (!_active)
        {
            return;
        }

        var (width, height) = GetDimensions();
        bool clearFirst = width != _lastWidth || height != _lastHeight;
        _lastWidth = width;
        _lastHeight = height;

        if (width < MinimumWidth || height < MinimumHeight)
        {
            RenderTooSmall(width, height);
            return;
        }

        string[] lines = canvas.RenderLines();
        int left = (width - canvas.Width) / 2;
        int top = (height - canvas.Height) / 2;

        var output = new StringBuilder(lines.Length * (canvas.Width + 40) + 64);
        if (clearFirst)
        {
            output.Append("\x1b[2J");
        }

        output.Append("\x1b[H");

        for (int row = 0; row < top; row++)
        {
            output.Append("\x1b[K\r\n");
        }

        for (int row = 0; row < lines.Length; row++)
        {
            if (left > 0)
            {
                output.Append(' ', left);
            }

            output.Append(lines[row]);
            output.Append("\x1b[0m\x1b[K");

            if (row < lines.Length - 1)
            {
                output.Append("\r\n");
            }
        }

        // Clear any leftovers below the panel without moving the cursor past
        // the final terminal row (which would scroll the whole panel).
        output.Append("\x1b[J");
        Console.Write(output.ToString());
    }

    private void RenderTooSmall(int width, int height)
    {
        string[] messages =
        [
            "TERMINAL TOO SMALL",
            $"SNAKE NEEDS AT LEAST {MinimumWidth} x {MinimumHeight}",
            $"CURRENT WINDOW: {width} x {height}",
            "RESIZE THE WINDOW TO CONTINUE",
            "ESC: QUIT",
        ];

        int maxTextWidth = Math.Max(0, width - 2);
        int firstRow = Math.Max(0, (height - messages.Length) / 2);
        var output = new StringBuilder();
        output.Append("\x1b[2J\x1b[H");

        for (int row = 0; row < firstRow; row++)
        {
            output.Append("\x1b[K\r\n");
        }

        for (int i = 0; i < messages.Length; i++)
        {
            string message = messages[i].Length > maxTextWidth
                ? messages[i][..Math.Max(0, maxTextWidth)]
                : messages[i];

            int left = Math.Max(0, (width - message.Length) / 2);
            if (_useColor)
            {
                output.Append(i == 0 ? "\x1b[1;38;2;255;107;107m" : "\x1b[38;2;176;196;187m");
            }

            if (left > 0)
            {
                output.Append(' ', left);
            }

            output.Append(message);
            output.Append("\x1b[0m\x1b[K");

            if (i < messages.Length - 1 && firstRow + i < height - 1)
            {
                output.Append("\r\n");
            }
        }

        if (_useColor)
        {
            output.Append("\x1b[0m");
        }

        Console.Write(output.ToString());
    }

    public void Dispose()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        try
        {
            Console.Write("\x1b[0m\x1b[?7h\x1b[?25h\x1b[?1049l");
        }
        catch (IOException)
        {
        }
    }
}

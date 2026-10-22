namespace Snake.Ui;

public enum GameOverlay
{
    None,
    Paused,
    GameOver,
    Won,
}

/// <summary>
/// Composes all game screens on a fixed 74 x 22 cell canvas. Keeping the
/// canvas fixed means the board never shifts while playing; only the panel
/// position changes with the terminal size.
/// </summary>
public sealed class GameRenderer
{
    public const int CanvasWidth = SnakeGame.Width * 2 + 2;
    public const int CanvasHeight = SnakeGame.Height + 6;

    private const int BoardTop = 3;
    private const int BoardHeight = SnakeGame.Height + 2;
    private const int InteriorLeft = 1;
    private const int InteriorTop = 4;
    private const int InteriorWidth = SnakeGame.Width * 2;
    private const int FooterRow = CanvasHeight - 1;

    private readonly bool _useColor;

    public GameRenderer(bool useColor)
    {
        _useColor = useColor;
    }

    public Canvas BuildMenu(Difficulty selected, Func<Difficulty, int> bestFor)
    {
        var canvas = new Canvas(CanvasWidth, CanvasHeight, _useColor);
        DrawLogo(canvas);
        DrawCenteredRuns(
            canvas,
            1,
            new TextRun("A TINY TERMINAL ARCADE", Style(Theme.Muted, dim: true)));
        DrawBoard(canvas);

        DrawCenteredInBoard(canvas, 5, "S E L E C T   Y O U R   P A C E", Style(Theme.Muted, dim: true));

        const int cardWidth = 20;
        const int cardHeight = 5;
        const int cardGap = 4;
        int cardsWidth = cardWidth * DifficultyProfile.All.Count + cardGap * (DifficultyProfile.All.Count - 1);
        int cardStartX = InteriorLeft + (InteriorWidth - cardsWidth) / 2;
        const int cardTop = 7;

        for (int index = 0; index < DifficultyProfile.All.Count; index++)
        {
            Difficulty difficulty = DifficultyProfile.All[index];
            DrawDifficultyCard(
                canvas,
                cardStartX + index * (cardWidth + cardGap),
                cardTop,
                cardWidth,
                cardHeight,
                index + 1,
                difficulty,
                bestFor(difficulty),
                difficulty == selected);
        }

        DrawCenteredRuns(
            canvas,
            13,
            new TextRun("PRESS ", Style(Theme.Muted)),
            new TextRun("  SPACE  ", Style(Theme.Ink, Theme.Mint, bold: true)),
            new TextRun(" TO START", Style(Theme.Text, bold: true)));

        DrawCenteredRuns(
            canvas,
            15,
            new TextRun("LEFT / RIGHT", Style(Theme.Mint)),
            new TextRun("  CHOOSE     ", Style(Theme.Muted)),
            new TextRun("1 / 2 / 3", Style(Theme.Mint)),
            new TextRun("  QUICK PICK", Style(Theme.Muted)));

        DrawCenteredRuns(
            canvas,
            17,
            new TextRun("EAT ", Style(Theme.Muted)),
            new TextRun("◆", Style(Theme.Food, bold: true)),
            new TextRun(" TO GROW       WALLS ARE SOLID", Style(Theme.Muted)));

        DrawFooter(
            canvas,
            new TextRun("LEFT / RIGHT", Style(Theme.Mint)),
            new TextRun("  SELECT     ", Style(Theme.Muted)),
            new TextRun("SPACE", Style(Theme.Mint)),
            new TextRun("  START     ", Style(Theme.Muted)),
            new TextRun("ESC", Style(Theme.Mint)),
            new TextRun("  QUIT", Style(Theme.Muted)));

        return canvas;
    }

    public Canvas BuildGame(SnakeGame game, int best, GameOverlay overlay, bool newBest)
    {
        var canvas = new Canvas(CanvasWidth, CanvasHeight, _useColor);
        DrawLogo(canvas);

        string mode = DifficultyProfile.For(game.Difficulty).Name;
        canvas.Write(CanvasWidth - mode.Length - 1, 0, mode, Style(Theme.Muted, dim: true));

        DrawStats(canvas, game, best);
        DrawBoard(canvas);
        DrawPlayfield(canvas, game);

        switch (overlay)
        {
            case GameOverlay.Paused:
                DrawPausedOverlay(canvas);
                break;
            case GameOverlay.GameOver:
                DrawFinishedOverlay(canvas, game, best, newBest, won: false);
                break;
            case GameOverlay.Won:
                DrawFinishedOverlay(canvas, game, best, newBest, won: true);
                break;
        }

        DrawFooter(canvas, FooterRunsFor(overlay));
        return canvas;
    }

    private static TextRun[] FooterRunsFor(GameOverlay overlay) =>
        overlay switch
        {
            GameOverlay.Paused =>
            [
                new("SPACE / P", Style(Theme.Mint)),
                new("  RESUME     ", Style(Theme.Muted)),
                new("R", Style(Theme.Mint)),
                new("  RESTART     ", Style(Theme.Muted)),
                new("M", Style(Theme.Mint)),
                new("  MENU     ", Style(Theme.Muted)),
                new("Q", Style(Theme.Mint)),
                new("  QUIT", Style(Theme.Muted)),
            ],
            GameOverlay.GameOver or GameOverlay.Won =>
            [
                new("SPACE", Style(Theme.Mint)),
                new("  PLAY AGAIN     ", Style(Theme.Muted)),
                new("M", Style(Theme.Mint)),
                new("  MENU     ", Style(Theme.Muted)),
                new("Q", Style(Theme.Mint)),
                new("  QUIT", Style(Theme.Muted)),
            ],
            _ =>
            [
                new("WASD / ARROWS", Style(Theme.Mint)),
                new("  MOVE     ", Style(Theme.Muted)),
                new("P / ESC", Style(Theme.Mint)),
                new("  PAUSE     ", Style(Theme.Muted)),
                new("CTRL+C", Style(Theme.Mint)),
                new("  QUIT", Style(Theme.Muted)),
            ],
        };

    private void DrawLogo(Canvas canvas)
    {
        DrawCenteredRuns(
            canvas,
            0,
            new TextRun("◆", Style(Theme.Food, bold: true)),
            new TextRun("   S N A K E   ", Style(Theme.MintBright, bold: true)),
            new TextRun("◆", Style(Theme.Food, bold: true)));
    }

    private void DrawStats(Canvas canvas, SnakeGame game, int best)
    {
        DrawCenteredRuns(
            canvas,
            1,
            new TextRun("SCORE ", Style(Theme.Muted)),
            new TextRun(game.Score.ToString("D4"), Style(Theme.Text, bold: true)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("BEST ", Style(Theme.Muted)),
            new TextRun(best.ToString("D4"), Style(Theme.Gold, bold: true)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("LEVEL ", Style(Theme.Muted)),
            new TextRun(game.Level.ToString("D2"), Style(Theme.Mint)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("LENGTH ", Style(Theme.Muted)),
            new TextRun(game.Length.ToString("D2"), Style(Theme.Text)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("SPEED ", Style(Theme.Muted)),
            new TextRun($"{game.SpeedMultiplier:0.0}x", Style(Theme.Mint)));
    }

    private static void DrawBoard(Canvas canvas)
    {
        canvas.DrawBorder(
            0,
            BoardTop,
            CanvasWidth,
            BoardHeight,
            new AnsiStyle(Theme.Border));
    }

    private void DrawPlayfield(Canvas canvas, SnakeGame game)
    {
        var snakeCells = new Dictionary<Cell, int>(game.Length);
        for (int index = 0; index < game.Snake.Count; index++)
        {
            snakeCells[game.Snake[index]] = index;
        }

        for (int y = 0; y < SnakeGame.Height; y++)
        {
            for (int x = 0; x < SnakeGame.Width; x++)
            {
                var cell = new Cell(x, y);
                int column = InteriorLeft + x * 2;
                int row = InteriorTop + y;

                if (snakeCells.TryGetValue(cell, out int index))
                {
                    if (index == 0)
                    {
                        AnsiStyle headStyle = new(Theme.SnakeHead, Bold: true);
                        canvas.Set(column, row, '◉', headStyle);
                        canvas.Set(column + 1, row, '◉', headStyle);
                    }
                    else
                    {
                        double amount = index / (double)Math.Max(1, game.Length - 1);
                        Rgb color = Rgb.Lerp(Theme.SnakeBodyNear, Theme.SnakeBodyFar, amount);
                        AnsiStyle bodyStyle = new(color);
                        canvas.Set(column, row, '█', bodyStyle);
                        canvas.Set(column + 1, row, '█', bodyStyle);
                    }
                }
                else if (game.Food is { } food && food == cell)
                {
                    canvas.Set(column, row, '◆', new AnsiStyle(Theme.Food, Bold: true));
                }
            }
        }
    }

    private void DrawDifficultyCard(
        Canvas canvas,
        int x,
        int y,
        int width,
        int height,
        int number,
        Difficulty difficulty,
        int best,
        bool selected)
    {
        Rgb background = selected ? Theme.SelectedCard : Theme.Card;
        Rgb border = selected ? Theme.Mint : Theme.Border;
        AnsiStyle backgroundStyle = new(Background: background);
        AnsiStyle borderStyle = new(border, background);

        if (selected)
        {
            canvas.Write(x + width / 2 - 1, y - 1, "▼", new AnsiStyle(Theme.Mint, Bold: true));
        }

        // A one-cell drop shadow gives the cards a little depth on dark terminals.
        canvas.FillRect(x + 1, y + 1, width, height, ' ', new AnsiStyle(Background: Theme.Shadow));
        canvas.FillRect(x, y, width, height, ' ', backgroundStyle);
        canvas.DrawBorder(x, y, width, height, borderStyle);

        DifficultyProfile profile = DifficultyProfile.For(difficulty);
        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            y + 1,
            new TextRun($"{number}", Style(Theme.Gold, background, bold: true)),
            new TextRun("  ", Style(Theme.Muted, background)),
            new TextRun(profile.Name, Style(selected ? Theme.MintBright : Theme.Muted, background, bold: selected)));

        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            y + 2,
            new TextRun(
                profile.Tagline,
                Style(selected ? Theme.Text : Theme.Muted, background, dim: !selected)));

        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            y + 3,
            new TextRun("BEST ", Style(Theme.Muted, background, dim: !selected)),
            new TextRun(best.ToString("D4"), Style(selected ? Theme.Gold : Theme.Muted, background, bold: selected)));
    }

    private void DrawPausedOverlay(Canvas canvas)
    {
        canvas.DimRegion(InteriorLeft, InteriorTop, InteriorWidth, SnakeGame.Height);

        const int width = 46;
        const int height = 8;
        (int x, int y) = DrawPanel(canvas, width, height, Theme.Gold);

        DrawPanelCentered(canvas, x, width, y + 1, "P A U S E D", Theme.Gold, bold: true);

        int optionX = x + 13;
        DrawPanelOption(canvas, optionX, y + 3, "SPACE / P", "RESUME");
        DrawPanelOption(canvas, optionX, y + 4, "R", "RESTART");
        DrawPanelOption(canvas, optionX, y + 5, "M", "MENU");
        DrawPanelOption(canvas, optionX, y + 6, "Q", "QUIT");
    }

    private void DrawFinishedOverlay(
        Canvas canvas,
        SnakeGame game,
        int best,
        bool newBest,
        bool won)
    {
        canvas.DimRegion(InteriorLeft, InteriorTop, InteriorWidth, SnakeGame.Height);

        const int width = 50;
        int height = newBest ? 9 : 8;
        Rgb accent = won ? Theme.Gold : Theme.Food;
        (int x, int y) = DrawPanel(canvas, width, height, accent);

        string title = won ? "BOARD CLEARED!" : "GAME OVER";
        DrawPanelCentered(canvas, x, width, y + 1, title, accent, bold: true);

        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            y + 3,
            new TextRun("SCORE ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(game.Score.ToString("D4"), Style(Theme.Text, Theme.Panel, bold: true)),
            new TextRun("     BEST ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(best.ToString("D4"), Style(Theme.Gold, Theme.Panel, bold: true)));

        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            y + 4,
            new TextRun("LEVEL ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(game.Level.ToString("D2"), Style(Theme.Mint, Theme.Panel)),
            new TextRun("     LENGTH ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(game.Length.ToString("D2"), Style(Theme.Text, Theme.Panel)));

        if (newBest)
        {
            DrawPanelCentered(canvas, x, width, y + 6, "NEW HIGH SCORE!", Theme.Gold, bold: true);
        }

        int actionRow = y + (newBest ? 7 : 6);
        DrawRunsCenteredIn(
            canvas,
            x + 1,
            width - 2,
            actionRow,
            new TextRun("SPACE", Style(Theme.Mint, Theme.Panel, bold: true)),
            new TextRun("  AGAIN      ", Style(Theme.Muted, Theme.Panel)),
            new TextRun("M", Style(Theme.Mint, Theme.Panel, bold: true)),
            new TextRun("  MENU      ", Style(Theme.Muted, Theme.Panel)),
            new TextRun("Q", Style(Theme.Mint, Theme.Panel, bold: true)),
            new TextRun("  QUIT", Style(Theme.Muted, Theme.Panel)));
    }

    private (int X, int Y) DrawPanel(Canvas canvas, int width, int height, Rgb border)
    {
        int x = InteriorLeft + (InteriorWidth - width) / 2;
        int y = InteriorTop + (SnakeGame.Height - height) / 2;

        canvas.FillRect(
            x + 1,
            y + 1,
            width,
            height,
            ' ',
            new AnsiStyle(Background: Theme.Shadow));

        canvas.FillRect(
            x,
            y,
            width,
            height,
            ' ',
            new AnsiStyle(Background: Theme.Panel));

        canvas.DrawBorder(
            x,
            y,
            width,
            height,
            new AnsiStyle(border, Theme.Panel));

        return (x, y);
    }

    private static void DrawPanelOption(Canvas canvas, int x, int y, string key, string action)
    {
        canvas.Write(x, y, key, Style(Theme.Mint, Theme.Panel, bold: true));
        canvas.Write(x + 12, y, action, Style(Theme.Text, Theme.Panel));
    }

    private static void DrawPanelCentered(
        Canvas canvas,
        int x,
        int width,
        int y,
        string text,
        Rgb color,
        bool bold = false)
    {
        int centeredX = x + (width - text.Length) / 2;
        canvas.Write(centeredX, y, text, Style(color, Theme.Panel, bold));
    }

    private void DrawFooter(Canvas canvas, params TextRun[] runs) =>
        DrawCenteredRuns(canvas, FooterRow, runs);

    private static void DrawCenteredInBoard(Canvas canvas, int y, string text, AnsiStyle style)
    {
        canvas.Write(InteriorLeft + (InteriorWidth - text.Length) / 2, y, text, style);
    }

    private static void DrawCenteredRuns(Canvas canvas, int y, params TextRun[] runs)
    {
        int width = 0;
        foreach (TextRun run in runs)
        {
            width += run.Text.Length;
        }

        int x = (canvas.Width - width) / 2;
        foreach (TextRun run in runs)
        {
            canvas.Write(x, y, run.Text, run.Style);
            x += run.Text.Length;
        }
    }

    private static void DrawRunsCenteredIn(
        Canvas canvas,
        int areaX,
        int areaWidth,
        int y,
        params TextRun[] runs)
    {
        int width = 0;
        foreach (TextRun run in runs)
        {
            width += run.Text.Length;
        }

        int x = areaX + (areaWidth - width) / 2;
        foreach (TextRun run in runs)
        {
            canvas.Write(x, y, run.Text, run.Style);
            x += run.Text.Length;
        }
    }

    private static AnsiStyle Style(Rgb foreground, bool bold = false, bool dim = false) =>
        new(foreground, Bold: bold, Dim: dim);

    private static AnsiStyle Style(Rgb foreground, Rgb background, bool bold = false, bool dim = false) =>
        new(foreground, background, bold, dim);

    private readonly record struct TextRun(string Text, AnsiStyle Style);

    private static class Theme
    {
        public static readonly Rgb Mint = new(113, 241, 154);
        public static readonly Rgb MintBright = new(219, 255, 231);
        public static readonly Rgb Text = new(213, 232, 223);
        public static readonly Rgb Muted = new(119, 145, 134);
        public static readonly Rgb Border = new(51, 79, 68);
        public static readonly Rgb Food = new(255, 99, 105);
        public static readonly Rgb Gold = new(255, 207, 102);
        public static readonly Rgb Ink = new(6, 25, 14);
        public static readonly Rgb SnakeHead = new(225, 255, 235);
        public static readonly Rgb SnakeBodyNear = new(101, 238, 145);
        public static readonly Rgb SnakeBodyFar = new(27, 116, 70);
        public static readonly Rgb Card = new(10, 17, 14);
        public static readonly Rgb SelectedCard = new(14, 35, 24);
        public static readonly Rgb Shadow = new(4, 7, 6);
        public static readonly Rgb Panel = new(12, 24, 18);
    }
}

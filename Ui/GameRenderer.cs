namespace Snake.Ui;

public enum GameOverlay
{
    None,
    Paused,
    RoundOver,
    OnlineRoundOver,
}

/// <summary>
/// Composes all game screens on a fixed 74 x 22 cell canvas. Keeping the
/// canvas fixed means the board never shifts while playing; only the panel
/// position changes with the terminal size.
/// </summary>
public sealed class GameRenderer
{
    public const int CanvasWidth = GameWorld.Width * 2 + 2;
    public const int CanvasHeight = GameWorld.Height + 6;

    private const int BoardTop = 3;
    private const int BoardHeight = GameWorld.Height + 2;
    private const int InteriorLeft = 1;
    private const int InteriorTop = 4;
    private const int InteriorWidth = GameWorld.Width * 2;
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
            new TextRun("TWO SNAKES · ONE BOARD", Style(Theme.Muted, dim: true)));
        DrawCenteredRuns(
            canvas,
            2,
            new TextRun("P1  WASD + E", Style(Theme.PlayerOne, bold: true)),
            new TextRun("      VS      ", Style(Theme.Muted, dim: true)),
            new TextRun("P2  ARROWS + O", Style(Theme.PlayerTwo, bold: true)));

        DrawBoard(canvas);

        DrawCenteredInBoard(canvas, 5, "S E L E C T   Y O U R   P A C E", Style(Theme.Muted, dim: true));

        const int cardWidth = 20;
        const int cardHeight = 5;
        const int cardGap = 4;
        int cardsWidth = cardWidth * DifficultyProfile.All.Count
            + cardGap * (DifficultyProfile.All.Count - 1);
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
            new TextRun("BOOST ", Style(Theme.Muted)),
            new TextRun("2s SURGE", Style(Theme.Gold, bold: true)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("7s COOLDOWN", Style(Theme.Muted)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("E / O", Style(Theme.Mint)));

        DrawCenteredRuns(
            canvas,
            17,
            new TextRun("EAT ", Style(Theme.Muted)),
            new TextRun("◆", Style(Theme.Food, bold: true)),
            new TextRun(" +10       LAST SNAKE SLITHERING WINS", Style(Theme.Muted)));

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

    public Canvas BuildGame(IGameView world, GameOverlay overlay, bool newBest)
    {
        var canvas = new Canvas(CanvasWidth, CanvasHeight, _useColor);
        DrawLogo(canvas);

        string mode = DifficultyProfile.For(world.Difficulty).Name;
        canvas.Write(CanvasWidth - mode.Length - 1, 0, mode, Style(Theme.Muted, dim: true));

        DrawPlayerHud(canvas, 1, world.Players[0]);
        DrawPlayerHud(canvas, 2, world.Players[1]);
        DrawBoard(canvas);
        DrawPlayfield(canvas, world);

        switch (overlay)
        {
            case GameOverlay.Paused:
                DrawPausedOverlay(canvas);
                break;
            case GameOverlay.RoundOver:
                DrawRoundOverOverlay(canvas, world, newBest, online: false);
                break;
            case GameOverlay.OnlineRoundOver:
                DrawRoundOverOverlay(canvas, world, newBest: false, online: true);
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
            GameOverlay.RoundOver =>
            [
                new("SPACE", Style(Theme.Mint)),
                new("  REMATCH     ", Style(Theme.Muted)),
                new("M", Style(Theme.Mint)),
                new("  MENU     ", Style(Theme.Muted)),
                new("Q", Style(Theme.Mint)),
                new("  QUIT", Style(Theme.Muted)),
            ],
            GameOverlay.OnlineRoundOver =>
            [
                new("ENTER", Style(Theme.Mint)),
                new("  REMATCH     ", Style(Theme.Muted)),
                new("ESC", Style(Theme.Mint)),
                new("  QUIT", Style(Theme.Muted)),
            ],
            _ =>
            [
                new("P1 WASD + E", Style(Theme.PlayerOne)),
                new("  BOOST     ", Style(Theme.Muted)),
                new("P2 ARROWS + O", Style(Theme.PlayerTwo)),
                new("  BOOST     ", Style(Theme.Muted)),
                new("P / ESC", Style(Theme.Mint)),
                new("  PAUSE", Style(Theme.Muted)),
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

    private void DrawPlayerHud(Canvas canvas, int row, IPlayerView player)
    {
        PlayerColors colors = ColorsFor(player.Index);
        string key = player.Index == 0 ? "E" : "O";

        TextRun skillRun;
        if (!player.Alive)
        {
            skillRun = new TextRun("OUT", Style(Theme.Food, bold: true));
        }
        else if (player.IsBoostActive)
        {
            skillRun = new TextRun(
                $"BOOST {player.BoostActiveRemainingMs / 1000.0:0.0}s",
                Style(Theme.Gold, bold: true));
        }
        else if (player.BoostCooldownRemainingMs > 0)
        {
            skillRun = new TextRun(
                $"COOLDOWN {player.BoostCooldownRemainingMs / 1000.0:0.0}s",
                Style(Theme.Muted));
        }
        else
        {
            skillRun = new TextRun(
                $"BOOST READY [{key}]",
                Style(colors.Accent, bold: true));
        }

        DrawCenteredRuns(
            canvas,
            row,
            new TextRun($"P{player.Index + 1}", Style(colors.Accent, bold: true)),
            new TextRun("  SCORE ", Style(Theme.Muted)),
            new TextRun(player.Score.ToString("D4"), Style(Theme.Text, bold: true)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("LEN ", Style(Theme.Muted)),
            new TextRun(player.Length.ToString("D2"), Style(Theme.Text)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("LV ", Style(Theme.Muted)),
            new TextRun(player.Level.ToString("D2"), Style(Theme.Mint)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            new TextRun("SPEED ", Style(Theme.Muted)),
            new TextRun(
                $"{player.SpeedMultiplier:0.0}x",
                Style(player.IsBoostActive ? Theme.Gold : Theme.Text, bold: player.IsBoostActive)),
            new TextRun("  ·  ", Style(Theme.Border, dim: true)),
            skillRun);
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

    private void DrawPlayfield(Canvas canvas, IGameView world)
    {
        var snakeCells = new Dictionary<Cell, (int Player, int Index, int Count)>();

        foreach (IPlayerView player in world.Players)
        {
            for (int index = 0; index < player.Body.Count; index++)
            {
                snakeCells[player.Body[index]] = (player.Index, index, player.Body.Count);
            }
        }

        for (int y = 0; y < GameWorld.Height; y++)
        {
            for (int x = 0; x < GameWorld.Width; x++)
            {
                var cell = new Cell(x, y);
                int column = InteriorLeft + x * 2;
                int row = InteriorTop + y;

                if (snakeCells.TryGetValue(cell, out var snake))
                {
                    PlayerColors colors = ColorsFor(snake.Player);

                    if (snake.Index == 0)
                    {
                        AnsiStyle headStyle = new(colors.Head, Bold: true);
                        canvas.Set(column, row, '◉', headStyle);
                        canvas.Set(column + 1, row, '◉', headStyle);
                    }
                    else
                    {
                        double amount = snake.Index / (double)Math.Max(1, snake.Count - 1);
                        Rgb color = Rgb.Lerp(colors.BodyNear, colors.BodyFar, amount);
                        AnsiStyle bodyStyle = new(color);
                        canvas.Set(column, row, '█', bodyStyle);
                        canvas.Set(column + 1, row, '█', bodyStyle);
                    }
                }
                else if (world.Food is { } food && food == cell)
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
        canvas.DimRegion(InteriorLeft, InteriorTop, InteriorWidth, GameWorld.Height);

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

    private void DrawRoundOverOverlay(Canvas canvas, IGameView world, bool newBest, bool online)
    {
        canvas.DimRegion(InteriorLeft, InteriorTop, InteriorWidth, GameWorld.Height);

        const int width = 56;
        int height = newBest ? 9 : 8;
        Rgb accent = world.WinnerIndex switch
        {
            0 => Theme.PlayerOne,
            1 => Theme.PlayerTwo,
            _ => Theme.Gold,
        };
        string title = world.WinnerIndex switch
        {
            0 => "PLAYER 1 WINS",
            1 => "PLAYER 2 WINS",
            _ => "DRAW",
        };

        (int x, int y) = DrawPanel(canvas, width, height, accent);
        DrawPanelCentered(canvas, x, width, y + 1, title, accent, bold: true);

        DrawPanelPlayerLine(canvas, x, width, y + 3, world.Players[0]);
        DrawPanelPlayerLine(canvas, x, width, y + 4, world.Players[1]);

        if (newBest)
        {
            DrawPanelCentered(canvas, x, width, y + 6, "NEW HIGH SCORE!", Theme.Gold, bold: true);
        }

        int actionRow = y + (newBest ? 7 : 6);
        if (online)
        {
            DrawRunsCenteredIn(
                canvas,
                x + 1,
                width - 2,
                actionRow,
                new TextRun("ENTER", Style(Theme.Mint, Theme.Panel, bold: true)),
                new TextRun("  REMATCH      ", Style(Theme.Muted, Theme.Panel)),
                new TextRun("ESC", Style(Theme.Mint, Theme.Panel, bold: true)),
                new TextRun("  QUIT", Style(Theme.Muted, Theme.Panel)));
        }
        else
        {
            DrawRunsCenteredIn(
                canvas,
                x + 1,
                width - 2,
                actionRow,
                new TextRun("SPACE", Style(Theme.Mint, Theme.Panel, bold: true)),
                new TextRun("  REMATCH      ", Style(Theme.Muted, Theme.Panel)),
                new TextRun("M", Style(Theme.Mint, Theme.Panel, bold: true)),
                new TextRun("  MENU      ", Style(Theme.Muted, Theme.Panel)),
                new TextRun("Q", Style(Theme.Mint, Theme.Panel, bold: true)),
                new TextRun("  QUIT", Style(Theme.Muted, Theme.Panel)));
        }
    }

    private static void DrawPanelPlayerLine(
        Canvas canvas,
        int panelX,
        int panelWidth,
        int y,
        IPlayerView player)
    {
        PlayerColors colors = ColorsFor(player.Index);
        DrawRunsCenteredIn(
            canvas,
            panelX + 1,
            panelWidth - 2,
            y,
            new TextRun($"P{player.Index + 1}", Style(colors.Accent, Theme.Panel, bold: true)),
            new TextRun("  SCORE ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(player.Score.ToString("D4"), Style(Theme.Text, Theme.Panel, bold: true)),
            new TextRun("    LEN ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(player.Length.ToString("D2"), Style(Theme.Text, Theme.Panel)),
            new TextRun("    LV ", Style(Theme.Muted, Theme.Panel)),
            new TextRun(player.Level.ToString("D2"), Style(Theme.Mint, Theme.Panel)));
    }

    private (int X, int Y) DrawPanel(Canvas canvas, int width, int height, Rgb border)
    {
        int x = InteriorLeft + (InteriorWidth - width) / 2;
        int y = InteriorTop + (GameWorld.Height - height) / 2;

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

    private static PlayerColors ColorsFor(int playerIndex) =>
        playerIndex == 0 ? Theme.PlayerOneColors : Theme.PlayerTwoColors;

    private readonly record struct TextRun(string Text, AnsiStyle Style);

    private readonly record struct PlayerColors(Rgb Accent, Rgb Head, Rgb BodyNear, Rgb BodyFar);

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
        public static readonly Rgb Card = new(10, 17, 14);
        public static readonly Rgb SelectedCard = new(14, 35, 24);
        public static readonly Rgb Shadow = new(4, 7, 6);
        public static readonly Rgb Panel = new(12, 24, 18);

        public static readonly Rgb PlayerOne = new(113, 241, 154);
        public static readonly Rgb PlayerTwo = new(99, 216, 255);

        public static readonly PlayerColors PlayerOneColors = new(
            PlayerOne,
            new Rgb(226, 255, 235),
            new Rgb(96, 235, 140),
            new Rgb(24, 110, 65));

        public static readonly PlayerColors PlayerTwoColors = new(
            PlayerTwo,
            new Rgb(226, 251, 255),
            new Rgb(88, 204, 244),
            new Rgb(20, 88, 118));
    }
}

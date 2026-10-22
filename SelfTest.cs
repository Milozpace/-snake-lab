using Snake.Ui;

namespace Snake;

internal static class SelfTest
{
    private static int _passed;
    private static readonly List<string> Failures = [];

    public static int Run()
    {
        Console.WriteLine("Running SNAKE self-test...");
        Console.WriteLine();

        Test("initial snake and food", () =>
        {
            var game = NewGame();
            AssertEqual(3, game.Length, "initial length");
            AssertEqual(Direction.Right, game.CurrentDirection, "initial direction");
            Assert(game.Food is not null, "food should be spawned");
            Assert(!game.Snake.Contains(game.Food!.Value), "food must not overlap the snake");
        });

        Test("moving forward keeps length and moves the head", () =>
        {
            var game = NewGame();
            Cell head = game.Snake[0];
            game.Advance();
            AssertEqual(head.X + 1, game.Snake[0].X, "head x");
            AssertEqual(head.Y, game.Snake[0].Y, "head y");
            AssertEqual(3, game.Length, "length");
        });

        Test("reversing into itself is rejected", () =>
        {
            var game = NewGame();
            Assert(!game.TryTurn(Direction.Left), "a 180° turn must be rejected");
            Assert(game.TryTurn(Direction.Up), "a quarter turn should be accepted");
            Assert(!game.TryTurn(Direction.Down), "opposite of a queued turn must be rejected");
        });

        Test("queued turns apply one per tick", () =>
        {
            var game = NewGame();
            Cell head = game.Snake[0];
            Assert(game.TryTurn(Direction.Up), "queue up");
            Assert(game.TryTurn(Direction.Right), "queue right");

            game.Advance();
            AssertEqual(head.X, game.Snake[0].X, "x after first turn");
            AssertEqual(head.Y - 1, game.Snake[0].Y, "y after first turn");

            game.Advance();
            AssertEqual(head.X + 1, game.Snake[0].X, "x after second turn");
            AssertEqual(head.Y - 1, game.Snake[0].Y, "y after second turn");
        });

        Test("hitting a wall ends the run", () =>
        {
            var game = NewGame();
            Assert(game.TryTurn(Direction.Up), "turn up");
            for (int i = 0; i < 100 && !game.IsGameOver; i++)
            {
                game.Advance();
            }

            Assert(game.IsGameOver, "wall collision should end the game");
            AssertEqual(0, game.Snake[0].Y, "head should stop at the wall");
        });

        Test("hitting the body ends the run", () =>
        {
            var game = NewGame();

            GrowForward(game, 2);
            Assert(game.TryTurn(Direction.Up), "turn up");
            game.SetFoodForTesting(game.Snake[0] + Direction.Up);
            game.Advance();

            Assert(game.TryTurn(Direction.Left), "turn left");
            game.Advance();
            Assert(game.TryTurn(Direction.Down), "turn down");
            game.Advance();

            Assert(game.IsGameOver, "body collision should end the game");
        });

        Test("eating scores points, grows the snake, and raises the level", () =>
        {
            var game = NewGame();
            int startingInterval = game.TickIntervalMs;

            for (int i = 0; i < 4; i++)
            {
                Cell next = game.Snake[0] + game.CurrentDirection;
                game.SetFoodForTesting(next);
                int scoreBefore = game.Score;
                int lengthBefore = game.Length;

                game.Advance();

                AssertEqual(scoreBefore + SnakeGame.PointsPerFood, game.Score, "score after food");
                AssertEqual(lengthBefore + 1, game.Length, "length after food");
            }

            AssertEqual(2, game.Level, "level after four foods");
            Assert(game.TickIntervalMs < startingInterval, "tick interval should shrink with level");
        });

        Test("menu and overlay layouts stay within the canvas", () =>
        {
            var renderer = new GameRenderer(useColor: false);
            var menu = renderer.BuildMenu(Difficulty.Classic, _ => 120);
            AssertCanvas(menu);

            string menuText = string.Join('\n', menu.RenderLines());
            Assert(menuText.Contains("CHILL", StringComparison.Ordinal), "menu should show CHILL");
            Assert(menuText.Contains("TURBO", StringComparison.Ordinal), "menu should show TURBO");

            var game = NewGame();
            Canvas paused = renderer.BuildGame(game, 120, GameOverlay.Paused, newBest: false);
            AssertCanvas(paused);
            Assert(
                string.Join('\n', paused.RenderLines()).Contains("P A U S E D", StringComparison.Ordinal),
                "pause overlay should be visible");

            Canvas finished = renderer.BuildGame(game, 120, GameOverlay.GameOver, newBest: true);
            AssertCanvas(finished);
            string finishedText = string.Join('\n', finished.RenderLines());
            Assert(finishedText.Contains("GAME OVER", StringComparison.Ordinal), "game-over title");
            Assert(finishedText.Contains("NEW HIGH SCORE!", StringComparison.Ordinal), "new-record banner");
        });

        Test("high scores persist", () =>
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"snake-selftest-{Guid.NewGuid():N}.txt");

            try
            {
                var store = new HighScoreStore(path);
                AssertEqual(0, store.GetBest(Difficulty.Classic), "initial best");
                Assert(store.UpdateBest(Difficulty.Classic, 120), "first score should be a record");
                Assert(!store.UpdateBest(Difficulty.Classic, 80), "lower score should not replace best");
                AssertEqual(120, store.GetBest(Difficulty.Classic), "updated best");

                var reloaded = new HighScoreStore(path);
                AssertEqual(120, reloaded.GetBest(Difficulty.Classic), "persisted best");
                AssertEqual(0, reloaded.GetBest(Difficulty.Turbo), "other mode remains empty");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        });

        Console.WriteLine();
        if (Failures.Count > 0)
        {
            Console.WriteLine($"{Failures.Count} test(s) failed:");
            foreach (string failure in Failures)
            {
                Console.WriteLine($"  - {failure}");
            }

            return 1;
        }

        Console.WriteLine($"All {_passed} tests passed.");
        return 0;
    }

    private static SnakeGame NewGame() => new(Difficulty.Classic, new Random(123456));

    private static void GrowForward(SnakeGame game, int meals)
    {
        for (int i = 0; i < meals; i++)
        {
            game.SetFoodForTesting(game.Snake[0] + game.CurrentDirection);
            game.Advance();
        }
    }

    private static void AssertCanvas(Canvas canvas)
    {
        AssertEqual(GameRenderer.CanvasWidth, canvas.Width, "canvas width");
        AssertEqual(GameRenderer.CanvasHeight, canvas.Height, "canvas height");

        string[] lines = canvas.RenderLines();
        AssertEqual(GameRenderer.CanvasHeight, lines.Length, "line count");
        foreach (string line in lines)
        {
            AssertEqual(GameRenderer.CanvasWidth, line.Length, "rendered line width");
        }
    }

    private static void Test(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"  PASS  {name}");
        }
        catch (Exception exception)
        {
            Failures.Add($"{name}: {exception.Message}");
            Console.WriteLine($"  FAIL  {name}");
            Console.WriteLine($"        {exception.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }
}

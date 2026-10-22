using Snake.Ui;

namespace Snake;

internal static class SelfTest
{
    private const int StepMs = 5;

    private static int _passed;
    private static readonly List<string> Failures = [];

    public static int Run()
    {
        Console.WriteLine("Running SNAKE v2 self-test...");
        Console.WriteLine();

        Test("two-player arena starts correctly", () =>
        {
            GameWorld world = NewWorld();
            AssertEqual(2, world.Players.Count, "player count");
            Assert(world.Players.All(player => player.Alive), "both players should be alive");
            Assert(world.Players.All(player => player.Length == 3), "both snakes start with length 3");
            Assert(world.Players.All(player => player.IsBoostReady), "both boosts start ready");
            Assert(world.Food is not null, "food should be spawned");
            Assert(
                world.Players.All(player => !player.Body.Contains(world.Food!.Value)),
                "food must not overlap either snake");
        });

        Test("both players move independently and turn independently", () =>
        {
            GameWorld world = NewWorld();
            world.SetFoodForTesting(new Cell(0, 0));

            Assert(world.TryTurn(0, Direction.Up), "player 1 turn");
            Assert(world.TryTurn(1, Direction.Down), "player 2 turn");

            Advance(world, 150);

            AssertEqual(new Cell(8, 7), world.Players[0].Head, "player 1 head");
            AssertEqual(new Cell(27, 9), world.Players[1].Head, "player 2 head");
        });

        Test("food scores, grows, and respawns for the correct player", () =>
        {
            GameWorld world = NewWorld();
            world.SetFoodForTesting(world.Players[0].Head + Direction.Right);

            Advance(world, 150);

            AssertEqual(10, world.Players[0].Score, "player 1 score");
            AssertEqual(4, world.Players[0].Length, "player 1 length");
            AssertEqual(0, world.Players[1].Score, "player 2 score");
            AssertEqual(3, world.Players[1].Length, "player 2 length");
            AssertEqual(1, world.FoodEatenCount, "food eaten count");
            Assert(
                world.Players.All(player => !player.Body.Contains(world.Food!.Value)),
                "respawned food must not overlap a snake");
        });

        Test("players can reach different speeds through leveling", () =>
        {
            GameWorld world = NewWorld();

            for (int i = 0; i < 4; i++)
            {
                int scoreBefore = world.Players[0].Score;
                world.SetFoodForTesting(world.Players[0].Head + Direction.Right);
                AdvanceUntil(world, () => world.Players[0].Score > scoreBefore, 1_000);
            }

            AssertEqual(2, world.Players[0].Level, "player 1 level");
            AssertEqual(1, world.Players[1].Level, "player 2 level");
            Assert(
                world.Players[0].BaseMoveIntervalMs < world.Players[1].BaseMoveIntervalMs,
                "leveled player should have a shorter base interval");
            AssertEqual(0, world.Players[1].Score, "player 2 score");
        });

        Test("boost is active briefly and has an independent cooldown", () =>
        {
            GameWorld world = NewWorld();
            SnakePlayer player = world.Players[0];
            int baseInterval = player.BaseMoveIntervalMs;

            Assert(world.TryActivateBoost(0), "boost should activate");
            Assert(player.IsBoostActive, "boost should be active");
            Assert(player.CurrentMoveIntervalMs < baseInterval, "boost should shorten movement interval");
            Assert(!world.TryActivateBoost(0), "active boost cannot be stacked");

            // Freeze movement after activation so this test is about the skill
            // clock only. Activation itself intentionally shortens the pending
            // movement cooldown.
            world.Players[0].MoveCooldownMs = 100_000;
            world.Players[1].MoveCooldownMs = 100_000;

            Advance(world, SnakePlayer.BoostDurationMs);
            Assert(!player.IsBoostActive, "boost duration should end");
            Assert(player.BoostCooldownRemainingMs > 0, "cooldown should remain");
            Assert(!player.IsBoostReady, "skill should not be ready during cooldown");

            Advance(world, SnakePlayer.BoostCooldownMs - SnakePlayer.BoostDurationMs);
            Assert(player.BoostCooldownRemainingMs == 0, "cooldown should reach zero");
            Assert(player.IsBoostReady, "skill should be ready again");
            Assert(world.TryActivateBoost(0), "boost should activate again");
        });

        Test("wall collision ends the round with the other player winning", () =>
        {
            GameWorld world = NewWorld();
            world.SetFoodForTesting(new Cell(0, 0));
            Assert(world.TryTurn(0, Direction.Up), "turn player 1 toward the wall");

            AdvanceUntil(world, () => world.IsOver, 3_000);

            Assert(world.IsOver, "round should end");
            Assert(!world.Players[0].Alive, "player 1 should be dead");
            Assert(world.Players[1].Alive, "player 2 should survive");
            AssertEqual(1, world.WinnerIndex, "player 2 should win");
        });

        Test("a head-on collision is a draw", () =>
        {
            GameWorld world = CreateDrawWorld();
            Assert(world.IsOver, "round should end");
            Assert(!world.Players[0].Alive && !world.Players[1].Alive, "both players should be dead");
            AssertEqual(-1, world.WinnerIndex, "winner index for a draw");
        });

        Test("two-player menu, HUD, pause, and result layouts fit", () =>
        {
            var renderer = new GameRenderer(useColor: false);
            Canvas menu = renderer.BuildMenu(Difficulty.Classic, _ => 120);
            AssertCanvas(menu);

            string menuText = string.Join('\n', menu.RenderLines());
            Assert(menuText.Contains("CHILL", StringComparison.Ordinal), "menu should show CHILL");
            Assert(menuText.Contains("TURBO", StringComparison.Ordinal), "menu should show TURBO");
            Assert(menuText.Contains("P1  WASD + E", StringComparison.Ordinal), "P1 controls on menu");
            Assert(menuText.Contains("P2  ARROWS + O", StringComparison.Ordinal), "P2 controls on menu");

            GameWorld world = NewWorld();
            Canvas game = renderer.BuildGame(world, GameOverlay.None, newBest: false);
            AssertCanvas(game);
            string gameText = string.Join('\n', game.RenderLines());
            Assert(gameText.Contains("BOOST READY [E]", StringComparison.Ordinal), "P1 skill HUD");
            Assert(gameText.Contains("BOOST READY [O]", StringComparison.Ordinal), "P2 skill HUD");

            Canvas paused = renderer.BuildGame(world, GameOverlay.Paused, newBest: false);
            AssertCanvas(paused);
            Assert(
                string.Join('\n', paused.RenderLines()).Contains("P A U S E D", StringComparison.Ordinal),
                "pause overlay should be visible");

            GameWorld draw = CreateDrawWorld();
            Canvas finished = renderer.BuildGame(draw, GameOverlay.RoundOver, newBest: true);
            AssertCanvas(finished);
            string finishedText = string.Join('\n', finished.RenderLines());
            Assert(finishedText.Contains("DRAW", StringComparison.Ordinal), "draw title");
            Assert(finishedText.Contains("NEW HIGH SCORE!", StringComparison.Ordinal), "new-record banner");
            Assert(finishedText.Contains("REMATCH", StringComparison.Ordinal), "rematch action");
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

    private static GameWorld NewWorld() => new(Difficulty.Classic, new Random(123456));

    private static GameWorld CreateDrawWorld()
    {
        GameWorld world = NewWorld();
        world.SetFoodForTesting(new Cell(0, 0));
        AdvanceUntil(world, () => world.IsOver, 4_000);
        return world;
    }

    private static void Advance(GameWorld world, int milliseconds)
    {
        for (int elapsed = 0; elapsed < milliseconds && !world.IsOver; elapsed += StepMs)
        {
            world.Update(StepMs);
        }
    }

    private static void AdvanceUntil(GameWorld world, Func<bool> condition, int maxMilliseconds)
    {
        for (int elapsed = 0; elapsed < maxMilliseconds && !world.IsOver && !condition(); elapsed += StepMs)
        {
            world.Update(StepMs);
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

using System.Diagnostics;
using Snake.Ui;

namespace Snake;

public sealed class GameApp : IDisposable
{
    private const int SimulationStepMs = 5;
    private const int MaxSimulationStepsPerFrame = 8;
    private const int HudRefreshMs = 100;

    private enum Screen
    {
        Menu,
        Playing,
        Paused,
        RoundOver,
    }

    private readonly HighScoreStore _scores;
    private readonly GameRenderer _renderer;
    private readonly TerminalSession _terminal;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Difficulty _selectedDifficulty = Difficulty.Classic;
    private GameWorld? _world;
    private Screen _screen = Screen.Menu;
    private bool _newBest;
    private bool _scoreCommitted;
    private bool _dirty = true;
    private bool _tooSmall;
    private volatile bool _quitRequested;
    private long _lastFrameMs;
    private long _simAccumulatorMs;
    private long _nextHudRefreshMs;
    private long _nextResizeCheckMs;
    private int _lastTerminalWidth;
    private int _lastTerminalHeight;

    public GameApp(bool useColor)
    {
        _scores = new HighScoreStore();
        _renderer = new GameRenderer(useColor);
        _terminal = new TerminalSession(useColor);
    }

    public int Run()
    {
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            _terminal.Enter();
            (_lastTerminalWidth, _lastTerminalHeight) = _terminal.GetDimensions();
            _tooSmall = _lastTerminalWidth < TerminalSession.MinimumWidth
                || _lastTerminalHeight < TerminalSession.MinimumHeight;

            long initialNow = _clock.ElapsedMilliseconds;
            _lastFrameMs = initialNow;
            _nextResizeCheckMs = initialNow + 100;
            _nextHudRefreshMs = initialNow;

            while (!_quitRequested)
            {
                PollInput();
                if (_quitRequested)
                {
                    break;
                }

                long now = _clock.ElapsedMilliseconds;

                if (now >= _nextResizeCheckMs)
                {
                    RefreshTerminalSize(now);
                    _nextResizeCheckMs = now + 100;
                }

                if (_screen == Screen.Playing && !_tooSmall && _world is not null)
                {
                    RunSimulation(now);

                    if (_world.IsOver)
                    {
                        FinishRound();
                    }

                    if (now >= _nextHudRefreshMs)
                    {
                        // The HUD contains boost/cooldown countdowns. Refreshing
                        // at 10 Hz keeps those smooth without rendering on every
                        // 5 ms simulation step.
                        _dirty = true;
                        _nextHudRefreshMs = now + HudRefreshMs;
                    }
                }
                else
                {
                    _lastFrameMs = now;
                    _simAccumulatorMs = 0;
                }

                if (_dirty)
                {
                    Render();
                    _dirty = false;
                }

                Thread.Sleep(2);
            }
        }
        catch (InvalidOperationException)
        {
            // Input can disappear if the controlling terminal is closed.
        }
        catch (IOException)
        {
            // Treat a broken terminal pipe as a normal exit.
        }
        finally
        {
            CommitScoresIfNeeded();
            Console.CancelKeyPress -= OnCancelKeyPress;
            _terminal.Dispose();
        }

        return 0;
    }

    private void RunSimulation(long now)
    {
        if (_world is null)
        {
            return;
        }

        long elapsed = Math.Max(0, now - _lastFrameMs);
        _lastFrameMs = now;
        _simAccumulatorMs += elapsed;

        int steps = 0;
        bool changed = false;

        while (_simAccumulatorMs >= SimulationStepMs
               && steps < MaxSimulationStepsPerFrame
               && !_world.IsOver)
        {
            changed |= _world.Update(SimulationStepMs);
            _simAccumulatorMs -= SimulationStepMs;
            steps++;
        }

        if (steps == MaxSimulationStepsPerFrame)
        {
            // Drop leftover backlog after a long stall instead of making the
            // snakes teleport through several cells on later frames.
            _simAccumulatorMs = 0;
        }

        if (changed)
        {
            _dirty = true;
        }
    }

    private void PollInput()
    {
        try
        {
            while (!_quitRequested && Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                if (_tooSmall)
                {
                    if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
                    {
                        _quitRequested = true;
                    }

                    continue;
                }

                HandleKey(key);
            }
        }
        catch (InvalidOperationException)
        {
            _quitRequested = true;
        }
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        switch (_screen)
        {
            case Screen.Menu:
                HandleMenuKey(key);
                break;
            case Screen.Playing:
                HandlePlayingKey(key);
                break;
            case Screen.Paused:
                HandlePausedKey(key);
                break;
            case Screen.RoundOver:
                HandleRoundOverKey(key);
                break;
        }
    }

    private void HandleMenuKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.A:
                CycleDifficulty(-1);
                break;
            case ConsoleKey.RightArrow:
            case ConsoleKey.D:
                CycleDifficulty(1);
                break;
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                SelectDifficulty(Difficulty.Chill);
                break;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                SelectDifficulty(Difficulty.Classic);
                break;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                SelectDifficulty(Difficulty.Turbo);
                break;
            case ConsoleKey.Spacebar:
            case ConsoleKey.Enter:
                StartGame();
                break;
            case ConsoleKey.Escape:
            case ConsoleKey.Q:
                _quitRequested = true;
                break;
        }
    }

    private void HandlePlayingKey(ConsoleKeyInfo key)
    {
        Direction? playerOneTurn = key.Key switch
        {
            ConsoleKey.W => Direction.Up,
            ConsoleKey.S => Direction.Down,
            ConsoleKey.A => Direction.Left,
            ConsoleKey.D => Direction.Right,
            _ => null,
        };

        if (playerOneTurn is { } turnOne && _world is not null)
        {
            _world.TryTurn(0, turnOne);
            return;
        }

        Direction? playerTwoTurn = key.Key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => null,
        };

        if (playerTwoTurn is { } turnTwo && _world is not null)
        {
            _world.TryTurn(1, turnTwo);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.E:
                if (_world?.TryActivateBoost(0) == true)
                {
                    _dirty = true;
                }

                break;
            case ConsoleKey.O:
                if (_world?.TryActivateBoost(1) == true)
                {
                    _dirty = true;
                }

                break;
            case ConsoleKey.P:
            case ConsoleKey.Escape:
                PauseGame();
                break;
            case ConsoleKey.Q:
                _quitRequested = true;
                break;
        }
    }

    private void HandlePausedKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
            case ConsoleKey.P:
            case ConsoleKey.Escape:
                ResumeGame();
                break;
            case ConsoleKey.R:
                StartGame();
                break;
            case ConsoleKey.M:
                ReturnToMenu();
                break;
            case ConsoleKey.Q:
                _quitRequested = true;
                break;
        }
    }

    private void HandleRoundOverKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
            case ConsoleKey.Enter:
            case ConsoleKey.R:
                StartGame();
                break;
            case ConsoleKey.M:
                ReturnToMenu();
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                _quitRequested = true;
                break;
        }
    }

    private void CycleDifficulty(int delta)
    {
        IReadOnlyList<Difficulty> all = DifficultyProfile.All;
        int current = 0;
        for (int index = 0; index < all.Count; index++)
        {
            if (all[index] == _selectedDifficulty)
            {
                current = index;
                break;
            }
        }

        int next = (current + delta + all.Count) % all.Count;
        SelectDifficulty(all[next]);
    }

    private void SelectDifficulty(Difficulty difficulty)
    {
        if (_selectedDifficulty == difficulty)
        {
            return;
        }

        _selectedDifficulty = difficulty;
        _dirty = true;
    }

    private void StartGame()
    {
        CommitScoresIfNeeded();

        _world = new GameWorld(_selectedDifficulty);
        _screen = Screen.Playing;
        _newBest = false;
        _scoreCommitted = false;

        long now = _clock.ElapsedMilliseconds;
        _lastFrameMs = now;
        _simAccumulatorMs = 0;
        _nextHudRefreshMs = now;
        _dirty = true;
    }

    private void PauseGame()
    {
        if (_screen != Screen.Playing || _world is null)
        {
            return;
        }

        _world.ClearPendingTurns();
        _screen = Screen.Paused;
        _simAccumulatorMs = 0;
        _dirty = true;
    }

    private void ResumeGame()
    {
        if (_screen != Screen.Paused)
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        _screen = Screen.Playing;
        _lastFrameMs = now;
        _simAccumulatorMs = 0;
        _nextHudRefreshMs = now;
        _dirty = true;
    }

    private void FinishRound()
    {
        if (_world is null)
        {
            return;
        }

        int bestScore = Math.Max(_world.Players[0].Score, _world.Players[1].Score);
        _newBest = _scores.UpdateBest(_world.Difficulty, bestScore);
        _scoreCommitted = true;
        _screen = Screen.RoundOver;
        _simAccumulatorMs = 0;
        _dirty = true;
    }

    private void ReturnToMenu()
    {
        CommitScoresIfNeeded();
        _world = null;
        _screen = Screen.Menu;
        _newBest = false;
        _dirty = true;
    }

    private void CommitScoresIfNeeded()
    {
        if (_world is null || _scoreCommitted)
        {
            return;
        }

        int bestScore = Math.Max(_world.Players[0].Score, _world.Players[1].Score);
        _scores.UpdateBest(_world.Difficulty, bestScore);
        _scoreCommitted = true;
    }

    private void RefreshTerminalSize(long now)
    {
        var (width, height) = _terminal.GetDimensions();
        bool small = width < TerminalSession.MinimumWidth || height < TerminalSession.MinimumHeight;

        if (width != _lastTerminalWidth || height != _lastTerminalHeight)
        {
            _lastTerminalWidth = width;
            _lastTerminalHeight = height;
            _dirty = true;
        }

        if (small != _tooSmall)
        {
            _tooSmall = small;
            if (!small)
            {
                _lastFrameMs = now;
                _simAccumulatorMs = 0;
            }

            _dirty = true;
        }
        else
        {
            _tooSmall = small;
        }
    }

    private void Render()
    {
        Canvas canvas;

        if (_screen == Screen.Menu)
        {
            canvas = _renderer.BuildMenu(_selectedDifficulty, _scores.GetBest);
        }
        else
        {
            if (_world is null)
            {
                return;
            }

            GameOverlay overlay = _screen switch
            {
                Screen.Paused => GameOverlay.Paused,
                Screen.RoundOver => GameOverlay.RoundOver,
                _ => GameOverlay.None,
            };

            canvas = _renderer.BuildGame(_world, overlay, _newBest);
        }

        _terminal.Render(canvas);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        _quitRequested = true;
    }

    public void Dispose() => _terminal.Dispose();
}

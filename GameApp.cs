using System.Diagnostics;
using Snake.Ui;

namespace Snake;

public sealed class GameApp : IDisposable
{
    private enum Screen
    {
        Menu,
        Playing,
        Paused,
        GameOver,
        Won,
    }

    private readonly HighScoreStore _scores;
    private readonly GameRenderer _renderer;
    private readonly TerminalSession _terminal;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Difficulty _selectedDifficulty = Difficulty.Classic;
    private SnakeGame? _game;
    private Screen _screen = Screen.Menu;
    private bool _newBest;
    private bool _scoreCommitted;
    private bool _dirty = true;
    private bool _tooSmall;
    private volatile bool _quitRequested;
    private long _lastTickMs;
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

                if (_screen == Screen.Playing && !_tooSmall && _game is not null)
                {
                    if (now - _lastTickMs >= _game.TickIntervalMs)
                    {
                        _game.Advance();
                        _lastTickMs = now;
                        _dirty = true;

                        if (_game.IsGameOver || _game.IsWon)
                        {
                            FinishGame();
                        }
                    }
                }

                if (_dirty)
                {
                    Render();
                    _dirty = false;
                }

                Thread.Sleep(3);
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
            CommitScoreIfNeeded();
            Console.CancelKeyPress -= OnCancelKeyPress;
            _terminal.Dispose();
        }

        return 0;
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
            case Screen.GameOver:
            case Screen.Won:
                HandleFinishedKey(key);
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
        Direction? direction = key.Key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W => Direction.Up,
            ConsoleKey.DownArrow or ConsoleKey.S => Direction.Down,
            ConsoleKey.LeftArrow or ConsoleKey.A => Direction.Left,
            ConsoleKey.RightArrow or ConsoleKey.D => Direction.Right,
            _ => null,
        };

        if (direction is { } turn && _game is not null)
        {
            _game.TryTurn(turn);
            return;
        }

        switch (key.Key)
        {
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

    private void HandleFinishedKey(ConsoleKeyInfo key)
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
        CommitScoreIfNeeded();

        _game = new SnakeGame(_selectedDifficulty);
        _screen = Screen.Playing;
        _newBest = false;
        _scoreCommitted = false;
        _lastTickMs = _clock.ElapsedMilliseconds;
        _dirty = true;
    }

    private void PauseGame()
    {
        if (_screen != Screen.Playing || _game is null)
        {
            return;
        }

        _game.ClearPendingTurns();
        _screen = Screen.Paused;
        _dirty = true;
    }

    private void ResumeGame()
    {
        if (_screen != Screen.Paused)
        {
            return;
        }

        _screen = Screen.Playing;
        _lastTickMs = _clock.ElapsedMilliseconds;
        _dirty = true;
    }

    private void FinishGame()
    {
        if (_game is null)
        {
            return;
        }

        _newBest = _scores.UpdateBest(_game.Difficulty, _game.Score);
        _scoreCommitted = true;
        _screen = _game.IsWon ? Screen.Won : Screen.GameOver;
        _dirty = true;
    }

    private void ReturnToMenu()
    {
        CommitScoreIfNeeded();
        _game = null;
        _screen = Screen.Menu;
        _newBest = false;
        _dirty = true;
    }

    private void CommitScoreIfNeeded()
    {
        if (_game is null || _scoreCommitted)
        {
            return;
        }

        _scores.UpdateBest(_game.Difficulty, _game.Score);
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
                _lastTickMs = now;
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
            if (_game is null)
            {
                return;
            }

            GameOverlay overlay = _screen switch
            {
                Screen.Paused => GameOverlay.Paused,
                Screen.GameOver => GameOverlay.GameOver,
                Screen.Won => GameOverlay.Won,
                _ => GameOverlay.None,
            };

            canvas = _renderer.BuildGame(
                _game,
                _scores.GetBest(_game.Difficulty),
                overlay,
                _newBest);
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

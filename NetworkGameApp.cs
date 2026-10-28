using System.Diagnostics;
using System.Text;
using Snake.Ui;

namespace Snake;

public sealed class NetworkGameApp : IDisposable
{
    private const int SimulationStepMs = 5;
    private const int MaxSimulationStepsPerFrame = 8;
    private const int RenderIntervalMs = 40;
    private const int SnapshotIntervalMs = 50;
    private const int OverSnapshotIntervalMs = 200;
    private const int HelloIntervalMs = 500;

    private readonly OnlineOptions _options;
    private readonly bool _useColor;
    private readonly TerminalSession _terminal;
    private readonly GameRenderer _renderer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string _hostTopic;
    private readonly string _guestTopic;

    private MqttTransport? _mqtt;
    private GameWorld? _hostWorld;
    private OnlineGameSnapshot? _guestSnapshot;
    private long _sequence;
    private long _lastGuestSequence = -1;
    private long _lastHostSimulationMs;
    private long _lastSnapshotMs;
    private long _lastRenderMs;
    private long _nextHelloMs;
    private long _simulationAccumulatorMs;
    private bool _guestJoined;
    private volatile bool _quitRequested;
    private string _status = "CONNECTING TO BROKER...";
    private string? _error;

    public NetworkGameApp(OnlineOptions options, bool useColor)
    {
        _options = options;
        _useColor = useColor;
        _terminal = new TerminalSession(useColor);
        _renderer = new GameRenderer(useColor);
        _hostTopic = NetworkProtocol.HostTopic(options.Room);
        _guestTopic = NetworkProtocol.GuestTopic(options.Room);
    }

    private OnlineRole Role => _options.Role;

    private string SubscribeTopic => Role == OnlineRole.Host ? _guestTopic : _hostTopic;

    private string PublishTopic => Role == OnlineRole.Host ? _hostTopic : _guestTopic;

    public int Run()
    {
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            _terminal.Enter();
            RenderLobby();

            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            string clientId = $"snake-{_options.Room}-{Role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}"[..32];
            _mqtt = MqttTransport.Connect(
                _options.BrokerHost,
                _options.BrokerPort,
                clientId,
                SubscribeTopic,
                connectTimeout.Token);

            if (Role == OnlineRole.Host)
            {
                _status = "WAITING FOR PLAYER 2...";
            }
            else
            {
                _status = "CONNECTING TO HOST...";
                SendHello();
                _nextHelloMs = _clock.ElapsedMilliseconds + HelloIntervalMs;
            }

            Render();

            while (!_quitRequested)
            {
                PollInput();
                DrainMessages();

                long now = _clock.ElapsedMilliseconds;
                if (Role == OnlineRole.Host)
                {
                    UpdateHost(now);
                }
                else
                {
                    UpdateGuest(now);
                }

                if (now - _lastRenderMs >= RenderIntervalMs)
                {
                    Render();
                    _lastRenderMs = now;
                }

                Thread.Sleep(2);
            }
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            _status = "CONNECTION ERROR";
            Render();
            WaitForExit();
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            _mqtt?.Dispose();
            _terminal.Dispose();
        }

        return 0;
    }

    private void PollInput()
    {
        try
        {
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                if (_error is not null)
                {
                    if (key.Key == ConsoleKey.Escape)
                    {
                        _quitRequested = true;
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Escape)
                {
                    _quitRequested = true;
                    continue;
                }

                if (Role == OnlineRole.Host)
                {
                    HandleHostKey(key);
                }
                else
                {
                    HandleGuestKey(key);
                }
            }
        }
        catch (InvalidOperationException)
        {
            _quitRequested = true;
        }
    }

    private void HandleHostKey(ConsoleKeyInfo key)
    {
        if (_hostWorld is null)
        {
            return;
        }

        if (_hostWorld.IsOver)
        {
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.R)
            {
                StartHostRound();
            }

            return;
        }

        Direction? direction = key.Key switch
        {
            ConsoleKey.W => Direction.Up,
            ConsoleKey.S => Direction.Down,
            ConsoleKey.A => Direction.Left,
            ConsoleKey.D => Direction.Right,
            _ => null,
        };

        if (direction is { } turn)
        {
            _hostWorld.TryTurn(0, turn);
        }
        else if (key.Key == ConsoleKey.E)
        {
            _hostWorld.TryActivateBoost(0);
        }
    }

    private void HandleGuestKey(ConsoleKeyInfo key)
    {
        if (_guestSnapshot is null)
        {
            return;
        }

        if (_guestSnapshot.IsOver)
        {
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.R)
            {
                SendRematch();
            }

            return;
        }

        Direction? direction = key.Key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => null,
        };

        if (direction is { } turn)
        {
            SendInput(turn, boost: false);
        }
        else if (key.Key == ConsoleKey.O)
        {
            SendInput(null, boost: true);
        }
    }

    private void DrainMessages()
    {
        if (_mqtt is null)
        {
            return;
        }

        while (_mqtt.TryDequeue(out NetworkEnvelope message))
        {
            if (!string.Equals(message.Room, _options.Room, StringComparison.Ordinal))
            {
                continue;
            }

            if (Role == OnlineRole.Host)
            {
                HandleHostMessage(message);
            }
            else
            {
                HandleGuestMessage(message);
            }
        }
    }

    private void HandleHostMessage(NetworkEnvelope message)
    {
        if (message.Type == NetworkMessageTypes.Hello)
        {
            if (!_guestJoined)
            {
                _guestJoined = true;
                StartHostRound();
            }

            return;
        }

        if (_hostWorld is null)
        {
            return;
        }

        if (message.Type == NetworkMessageTypes.Input && !_hostWorld.IsOver)
        {
            if (message.Direction is { } direction)
            {
                _hostWorld.TryTurn(1, direction);
            }

            if (message.Boost)
            {
                _hostWorld.TryActivateBoost(1);
            }

            return;
        }

        if (message.Type == NetworkMessageTypes.Rematch && _hostWorld.IsOver)
        {
            StartHostRound();
        }
    }

    private void HandleGuestMessage(NetworkEnvelope message)
    {
        if (message.Type != NetworkMessageTypes.State || message.State is null)
        {
            return;
        }

        if (message.State.Sequence <= _lastGuestSequence)
        {
            return;
        }

        _lastGuestSequence = message.State.Sequence;
        _guestSnapshot = message.State;
        _status = message.State.IsOver ? "ROUND OVER" : "MATCH IN PROGRESS";
    }

    private void StartHostRound()
    {
        _hostWorld = new GameWorld(_options.Difficulty);
        _sequence++;
        _lastHostSimulationMs = _clock.ElapsedMilliseconds;
        _simulationAccumulatorMs = 0;
        _lastSnapshotMs = 0;
        _status = "MATCH IN PROGRESS";
        PublishState();
    }

    private void UpdateHost(long now)
    {
        if (!_guestJoined || _hostWorld is null)
        {
            return;
        }

        if (!_hostWorld.IsOver)
        {
            long elapsed = Math.Max(0, now - _lastHostSimulationMs);
            _lastHostSimulationMs = now;
            _simulationAccumulatorMs += elapsed;

            int steps = 0;
            while (_simulationAccumulatorMs >= SimulationStepMs
                   && steps < MaxSimulationStepsPerFrame
                   && !_hostWorld.IsOver)
            {
                _hostWorld.Update(SimulationStepMs);
                _simulationAccumulatorMs -= SimulationStepMs;
                steps++;
            }

            if (steps == MaxSimulationStepsPerFrame)
            {
                _simulationAccumulatorMs = 0;
            }
        }

        int interval = _hostWorld.IsOver ? OverSnapshotIntervalMs : SnapshotIntervalMs;
        if (now - _lastSnapshotMs >= interval)
        {
            PublishState();
            _lastSnapshotMs = now;
        }
    }

    private void UpdateGuest(long now)
    {
        if (_guestSnapshot is not null)
        {
            return;
        }

        if (now >= _nextHelloMs)
        {
            SendHello();
            _nextHelloMs = now + HelloIntervalMs;
        }
    }

    private void PublishState()
    {
        if (_mqtt is null || _hostWorld is null)
        {
            return;
        }

        _sequence++;
        var envelope = new NetworkEnvelope
        {
            Type = NetworkMessageTypes.State,
            Room = _options.Room,
            State = OnlineGameSnapshot.Capture(_hostWorld, _sequence),
        };

        _mqtt.Publish(_hostTopic, envelope);
    }

    private void SendHello()
    {
        _mqtt?.Publish(_guestTopic, new NetworkEnvelope
        {
            Type = NetworkMessageTypes.Hello,
            Room = _options.Room,
        });
    }

    private void SendInput(Direction? direction, bool boost)
    {
        _mqtt?.Publish(_guestTopic, new NetworkEnvelope
        {
            Type = NetworkMessageTypes.Input,
            Room = _options.Room,
            Direction = direction,
            Boost = boost,
        });
    }

    private void SendRematch()
    {
        _mqtt?.Publish(_guestTopic, new NetworkEnvelope
        {
            Type = NetworkMessageTypes.Rematch,
            Room = _options.Room,
        });
    }

    private void RenderLobby() =>
        _terminal.Render(BuildLobbyCanvas(_status, isError: false));

    private void Render()
    {
        if (_error is not null)
        {
            _terminal.Render(BuildLobbyCanvas(_error, isError: true));
            return;
        }

        if (Role == OnlineRole.Host)
        {
            if (!_guestJoined || _hostWorld is null)
            {
                _terminal.Render(BuildLobbyCanvas(_status, isError: false));
                return;
            }

            GameOverlay overlay = _hostWorld.IsOver
                ? GameOverlay.OnlineRoundOver
                : GameOverlay.None;
            Canvas canvas = _renderer.BuildGame(_hostWorld, overlay, newBest: false);
            DrawOnlineFooter(canvas, _hostWorld.IsOver);
            _terminal.Render(canvas);
        }
        else
        {
            if (_guestSnapshot is null)
            {
                _terminal.Render(BuildLobbyCanvas(_status, isError: false));
                return;
            }

            GameOverlay overlay = _guestSnapshot.IsOver
                ? GameOverlay.OnlineRoundOver
                : GameOverlay.None;
            Canvas canvas = _renderer.BuildGame(_guestSnapshot, overlay, newBest: false);
            DrawOnlineFooter(canvas, _guestSnapshot.IsOver);
            _terminal.Render(canvas);
        }
    }

    private Canvas BuildLobbyCanvas(string status, bool isError)
    {
        var canvas = new Canvas(GameRenderer.CanvasWidth, GameRenderer.CanvasHeight, _useColor);

        canvas.WriteCentered(0, "◆   S N A K E   ◆", Fg(Theme.Mint, bold: true));
        canvas.WriteCentered(1, "M Q T T   O N L I N E", Fg(Theme.Muted, dim: true));

        const int panelWidth = 58;
        int panelHeight = 14;
        int panelX = (GameRenderer.CanvasWidth - panelWidth) / 2;
        int panelY = 4;

        canvas.FillRect(panelX, panelY, panelWidth, panelHeight, ' ', new AnsiStyle(Background: Theme.Panel));
        canvas.DrawBorder(
            panelX,
            panelY,
            panelWidth,
            panelHeight,
            new AnsiStyle(Theme.Border, Theme.Panel));

        void PanelText(int row, string text, AnsiStyle style)
        {
            int x = panelX + Math.Max(0, (panelWidth - text.Length) / 2);
            canvas.Write(x, row, text, style);
        }

        Rgb accentRgb = Role == OnlineRole.Host ? Theme.Mint : Theme.Cyan;

        PanelText(panelY + 1, "MQTT DUEL", Fg(Theme.Gold, bold: true));
        canvas.Write(
            panelX + 2,
            panelY + 2,
            new string('─', panelWidth - 4),
            new AnsiStyle(Theme.Border, Theme.Panel));

        PanelText(
            panelY + 4,
            $"ROOM   {_options.Room}",
            new AnsiStyle(accentRgb, Theme.Panel, Bold: true));

        PanelText(
            panelY + 5,
            $"BROKER   {_options.BrokerDisplay}",
            new AnsiStyle(Theme.Muted, Theme.Panel));

        string safeStatus = status.Length > panelWidth - 4
            ? status[..(panelWidth - 4)]
            : status;
        PanelText(
            panelY + 7,
            safeStatus,
            new AnsiStyle(isError ? Theme.Food : Theme.Text, Theme.Panel, Bold: true));

        string roleLine = Role == OnlineRole.Host
            ? "P1 HOST   WASD + E"
            : "P2 GUEST   ARROWS + O";
        PanelText(
            panelY + 9,
            roleLine,
            new AnsiStyle(accentRgb, Theme.Panel));

        PanelText(
            panelY + 11,
            "ESC   QUIT",
            new AnsiStyle(Theme.Muted, Theme.Panel, Dim: true));

        canvas.WriteCentered(
            GameRenderer.CanvasHeight - 1,
            "MQTT  ·  SHARED BOARD  ·  TWO PLAYERS",
            Fg(Theme.Muted, dim: true));

        return canvas;
    }

    private void DrawOnlineFooter(Canvas canvas, bool isOver)
    {
        int row = GameRenderer.CanvasHeight - 1;
        canvas.FillRect(0, row, GameRenderer.CanvasWidth, 1, ' ', AnsiStyle.Default);

        string text;
        if (isOver)
        {
            text = "ENTER  REMATCH      ESC  QUIT";
        }
        else if (Role == OnlineRole.Host)
        {
            text = "HOST P1  WASD + E      GUEST P2  ARROWS + O      ESC  QUIT";
        }
        else
        {
            text = "GUEST P2  ARROWS + O      ESC  QUIT";
        }

        int x = (GameRenderer.CanvasWidth - text.Length) / 2;
        canvas.Write(x, row, text, Fg(Theme.Mint));
    }

    private void WaitForExit()
    {
        while (!_quitRequested)
        {
            PollInput();
            Thread.Sleep(10);
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        _quitRequested = true;
    }

    public void Dispose() => _terminal.Dispose();

    private static AnsiStyle Fg(Rgb color, bool bold = false, bool dim = false) =>
        new(color, Bold: bold, Dim: dim);

    private static class Theme
    {
        public static readonly Rgb Mint = new(113, 241, 154);
        public static readonly Rgb Cyan = new(99, 216, 255);
        public static readonly Rgb Text = new(213, 232, 223);
        public static readonly Rgb Muted = new(119, 145, 134);
        public static readonly Rgb Border = new(51, 79, 68);
        public static readonly Rgb Food = new(255, 99, 105);
        public static readonly Rgb Gold = new(255, 207, 102);
        public static readonly Rgb Panel = new(12, 24, 18);
    }
}

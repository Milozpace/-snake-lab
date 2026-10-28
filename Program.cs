using System.Security.Cryptography;

namespace Snake;

internal static class Program
{
    private const string Version = "4.0.0";
    private const string DefaultBroker = "broker.emqx.io";
    private const int DefaultBrokerPort = 1883;
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        if (args.Contains("--version"))
        {
            Console.WriteLine($"SNAKE {Version}");
            return 0;
        }

        if (args.Contains("--self-test"))
        {
            return SelfTest.Run();
        }

        if (!TryParseOptions(args, out CommandOptions options, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            return 2;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("SNAKE needs an interactive terminal.");
            Console.Error.WriteLine("Run it directly in your terminal:  dotnet run");
            return 1;
        }

        bool noColorEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        bool dumbTerminal = string.Equals(
            Environment.GetEnvironmentVariable("TERM"),
            "dumb",
            StringComparison.OrdinalIgnoreCase);
        bool useColor = !options.NoColor && !noColorEnvironment && !dumbTerminal;

        try
        {
            if (options.Online is not null)
            {
                using var networkApp = new NetworkGameApp(options.Online, useColor);
                return networkApp.Run();
            }

            using var app = new GameApp(useColor);
            return app.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"SNAKE could not start: {exception.Message}");
            Console.Error.WriteLine("Try resizing the terminal or running: dotnet run -- --no-color");
            return 1;
        }
    }

    private static bool TryParseOptions(
        string[] args,
        out CommandOptions options,
        out string? error)
    {
        bool noColor = false;
        bool host = false;
        string? joinRoom = null;
        string? explicitRoom = null;
        string? broker = null;
        string? difficultyName = null;

        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case "--no-color":
                    noColor = true;
                    break;

                case "--host":
                    host = true;
                    break;

                case "--join":
                    if (!TryReadValue(args, ref i, out joinRoom))
                    {
                        options = default!;
                        error = "--join requires a room code.";
                        return false;
                    }

                    break;

                case "--room":
                    if (!TryReadValue(args, ref i, out explicitRoom))
                    {
                        options = default!;
                        error = "--room requires a room code.";
                        return false;
                    }

                    break;

                case "--broker":
                    if (!TryReadValue(args, ref i, out broker))
                    {
                        options = default!;
                        error = "--broker requires host[:port].";
                        return false;
                    }

                    break;

                case "--difficulty":
                    if (!TryReadValue(args, ref i, out difficultyName))
                    {
                        options = default!;
                        error = "--difficulty requires chill, classic, or turbo.";
                        return false;
                    }

                    break;

                default:
                    options = default!;
                    error = $"Unknown option: {argument}";
                    return false;
            }
        }

        if (host && joinRoom is not null)
        {
            options = default!;
            error = "Use either --host or --join, not both.";
            return false;
        }

        OnlineRole? role = host
            ? OnlineRole.Host
            : joinRoom is not null
                ? OnlineRole.Guest
                : null;

        if (role is null)
        {
            if (explicitRoom is not null || broker is not null || difficultyName is not null)
            {
                options = default!;
                error = "Online options require either --host or --join.";
                return false;
            }

            options = new CommandOptions(noColor, null);
            error = null;
            return true;
        }

        if (joinRoom is not null && explicitRoom is not null)
        {
            options = default!;
            error = "Do not combine --join with --room.";
            return false;
        }

        string room = role == OnlineRole.Host
            ? NormalizeRoom(explicitRoom ?? GenerateRoomCode())
            : NormalizeRoom(joinRoom!);

        if (room.Length is < 3 or > 16)
        {
            options = default!;
            error = "Room code must be 3-16 letters or digits.";
            return false;
        }

        string brokerValue = broker
            ?? Environment.GetEnvironmentVariable("SNAKE_MQTT_BROKER")
            ?? DefaultBroker;

        if (!TryParseBroker(brokerValue, out string brokerHost, out int brokerPort))
        {
            options = default!;
            error = "Broker must be host or host:port.";
            return false;
        }

        Difficulty difficulty = Difficulty.Classic;
        if (difficultyName is not null)
        {
            switch (difficultyName.Trim().ToLowerInvariant())
            {
                case "chill":
                    difficulty = Difficulty.Chill;
                    break;
                case "classic":
                    difficulty = Difficulty.Classic;
                    break;
                case "turbo":
                    difficulty = Difficulty.Turbo;
                    break;
                default:
                    options = default!;
                    error = "Difficulty must be chill, classic, or turbo.";
                    return false;
            }
        }

        options = new CommandOptions(
            noColor,
            new OnlineOptions(role.Value, room, brokerHost, brokerPort, difficulty));
        error = null;
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static string GenerateRoomCode()
    {
        Span<char> code = stackalloc char[6];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = RoomAlphabet[RandomNumberGenerator.GetInt32(RoomAlphabet.Length)];
        }

        return new string(code);
    }

    private static string NormalizeRoom(string room)
    {
        Span<char> buffer = stackalloc char[room.Length];
        int length = 0;

        foreach (char character in room.Trim().ToUpperInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                buffer[length++] = character;
            }
        }

        return new string(buffer[..length]);
    }

    private static bool TryParseBroker(string value, out string host, out int port)
    {
        value = value.Trim();
        if (value.StartsWith("mqtt://", StringComparison.OrdinalIgnoreCase))
        {
            value = value[7..];
        }
        else if (value.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            value = value[6..];
        }

        host = value;
        port = DefaultBrokerPort;

        int separator = value.LastIndexOf(':');
        if (separator > 0)
        {
            if (!int.TryParse(value[(separator + 1)..], out port)
                || port is < 1 or > 65535)
            {
                host = string.Empty;
                return false;
            }

            host = value[..separator];
        }

        return !string.IsNullOrWhiteSpace(host);
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"SNAKE {Version} — local and MQTT 5 online two-player battle");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run                                   Local two-player");
        Console.WriteLine("  dotnet run -- --host                         Create an MQTT room");
        Console.WriteLine("  dotnet run -- --join CODE                    Join an MQTT room");
        Console.WriteLine();
        Console.WriteLine("Online options:");
        Console.WriteLine("  --room CODE             Host room code (default: random)");
        Console.WriteLine("  --broker HOST[:PORT]    MQTT broker (default: broker.emqx.io:1883)");
        Console.WriteLine("  --difficulty NAME       chill, classic, or turbo (host only)");
        Console.WriteLine();
        Console.WriteLine("Other options:");
        Console.WriteLine("  --no-color              Play without colors");
        Console.WriteLine("  --self-test             Run built-in checks");
        Console.WriteLine("  --version               Show version");
        Console.WriteLine();
        Console.WriteLine("Controls:");
        Console.WriteLine("  Host / P1   W A S D to steer, E for boost");
        Console.WriteLine("  Guest / P2  arrow keys to steer, O for boost");
        Console.WriteLine("  Esc         quit");
        Console.WriteLine();
        Console.WriteLine("The public default broker is unencrypted. Use --broker localhost:1883");
        Console.WriteLine("or set SNAKE_MQTT_BROKER for a private/local broker.");
    }

    private sealed record CommandOptions(bool NoColor, OnlineOptions? Online);
}

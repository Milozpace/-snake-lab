namespace Snake;

internal static class Program
{
    private const string Version = "2.0.0";

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

        string? unknownArgument = args.FirstOrDefault(
            argument => !string.Equals(argument, "--no-color", StringComparison.Ordinal));

        if (unknownArgument is not null)
        {
            Console.Error.WriteLine($"Unknown option: {unknownArgument}");
            PrintHelp();
            return 2;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("SNAKE needs an interactive terminal.");
            Console.Error.WriteLine("Run it directly in your terminal:  dotnet run");
            return 1;
        }

        bool noColorFlag = args.Contains("--no-color");
        bool noColorEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        bool dumbTerminal = string.Equals(
            Environment.GetEnvironmentVariable("TERM"),
            "dumb",
            StringComparison.OrdinalIgnoreCase);
        bool useColor = !noColorFlag && !noColorEnvironment && !dumbTerminal;

        try
        {
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

    private static void PrintHelp()
    {
        Console.WriteLine($"SNAKE {Version} — a local two-player terminal battle");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run                 Play");
        Console.WriteLine("  dotnet run -- --no-color   Play without colors");
        Console.WriteLine("  dotnet run -- --self-test  Run the built-in logic and layout checks");
        Console.WriteLine("  dotnet run -- --version    Show the version");
        Console.WriteLine();
        Console.WriteLine("Controls:");
        Console.WriteLine("  Player 1   W A S D to steer, E for boost");
        Console.WriteLine("  Player 2   arrow keys to steer, O for boost");
        Console.WriteLine("  P / Esc    pause");
        Console.WriteLine("  Space      start / rematch");
        Console.WriteLine("  Ctrl+C     quit");
        Console.WriteLine();
        Console.WriteLine("Boost: 2 seconds of speed, 7 second cooldown.");
        Console.WriteLine("Scores are saved under your local application-data folder.");
    }
}

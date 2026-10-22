namespace Snake;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
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
            Console.Error.WriteLine("Try resizing the terminal or running: dotnet run --no-color");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("SNAKE — a terminal arcade game in C#");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run                 Play");
        Console.WriteLine("  dotnet run -- --no-color   Play without colors");
        Console.WriteLine("  dotnet run -- --self-test  Run the built-in logic and layout checks");
        Console.WriteLine();
        Console.WriteLine("Controls:");
        Console.WriteLine("  WASD / arrow keys   steer");
        Console.WriteLine("  P / Esc             pause");
        Console.WriteLine("  Space               start / play again");
        Console.WriteLine("  Ctrl+C              quit");
        Console.WriteLine();
        Console.WriteLine("Scores are saved under your local application-data folder.");
    }
}

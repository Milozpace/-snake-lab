namespace Snake;

public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}

public enum Difficulty
{
    Chill,
    Classic,
    Turbo,
}

public readonly record struct Cell(int X, int Y)
{
    public static Cell operator +(Cell cell, Direction direction) =>
        direction switch
        {
            Direction.Up => cell with { Y = cell.Y - 1 },
            Direction.Down => cell with { Y = cell.Y + 1 },
            Direction.Left => cell with { X = cell.X - 1 },
            Direction.Right => cell with { X = cell.X + 1 },
            _ => cell,
        };
}

public static class DirectionExtensions
{
    public static bool IsOpposite(this Direction direction, Direction other) =>
        (direction, other) switch
        {
            (Direction.Up, Direction.Down) => true,
            (Direction.Down, Direction.Up) => true,
            (Direction.Left, Direction.Right) => true,
            (Direction.Right, Direction.Left) => true,
            _ => false,
        };
}

public sealed record DifficultyProfile(
    Difficulty Difficulty,
    string Name,
    string Tagline,
    int BaseTickMs,
    int MinTickMs)
{
    public static DifficultyProfile For(Difficulty difficulty) =>
        difficulty switch
        {
            Difficulty.Chill => new(Difficulty.Chill, "CHILL", "SLOW & STEADY", 190, 100),
            Difficulty.Classic => new(Difficulty.Classic, "CLASSIC", "THE ORIGINAL", 145, 62),
            Difficulty.Turbo => new(Difficulty.Turbo, "TURBO", "NO MERCY", 100, 45),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
        };

    public int TickIntervalForLevel(int level) =>
        Math.Max(MinTickMs, BaseTickMs - (Math.Max(1, level) - 1) * 10);

    public static IReadOnlyList<Difficulty> All { get; } =
        new[] { Difficulty.Chill, Difficulty.Classic, Difficulty.Turbo };
}

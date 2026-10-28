using Snake.Protocol;

namespace Snake;

public static class NetworkProtocol
{
    public const int Version = 4;

    public static string HostTopic(string room) => $"snake/v4/{room}/host";

    public static string GuestTopic(string room) => $"snake/v4/{room}/guest";

    public static GameDifficulty ToProto(Difficulty difficulty) =>
        difficulty switch
        {
            Difficulty.Chill => GameDifficulty.Chill,
            Difficulty.Classic => GameDifficulty.Classic,
            Difficulty.Turbo => GameDifficulty.Turbo,
            _ => GameDifficulty.Unspecified,
        };

    public static Difficulty FromProto(GameDifficulty difficulty) =>
        difficulty switch
        {
            GameDifficulty.Chill => Difficulty.Chill,
            GameDifficulty.Turbo => Difficulty.Turbo,
            _ => Difficulty.Classic,
        };

    public static GameDirection ToProto(Direction direction) =>
        direction switch
        {
            Direction.Up => GameDirection.Up,
            Direction.Down => GameDirection.Down,
            Direction.Left => GameDirection.Left,
            Direction.Right => GameDirection.Right,
            _ => GameDirection.Unspecified,
        };

    public static Direction? FromProto(GameDirection direction) =>
        direction switch
        {
            GameDirection.Up => Snake.Direction.Up,
            GameDirection.Down => Snake.Direction.Down,
            GameDirection.Left => Snake.Direction.Left,
            GameDirection.Right => Snake.Direction.Right,
            _ => null,
        };
}

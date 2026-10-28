using Snake.Protocol;

namespace Snake.Protocol;

public sealed partial class WorldSnapshot : IGameView
{
    private IReadOnlyList<IPlayerView>? _playerViews;

    Difficulty IGameView.Difficulty => NetworkProtocol.FromProto(GameDifficulty);

    Snake.Cell? IGameView.Food => Food is null
        ? null
        : new Snake.Cell(Food.X, Food.Y);

    IReadOnlyList<IPlayerView> IGameView.Players =>
        _playerViews ??= Players.Cast<IPlayerView>().ToArray();

    public static WorldSnapshot Capture(GameWorld world, long sequence)
    {
        var snapshot = new WorldSnapshot
        {
            Sequence = sequence,
            GameTimeMs = world.GameTimeMs,
            GameDifficulty = NetworkProtocol.ToProto(world.Difficulty),
            IsOver = world.IsOver,
            WinnerIndex = world.WinnerIndex,
        };

        if (world.Food is { } food)
        {
            snapshot.Food = new Cell { X = food.X, Y = food.Y };
        }

        foreach (SnakePlayer player in world.Players)
        {
            var playerSnapshot = new PlayerSnapshot
            {
                Index = player.Index,
                Score = player.Score,
                Level = player.Level,
                Alive = player.Alive,
                IsBoostActive = player.IsBoostActive,
                BoostActiveRemainingMs = player.BoostActiveRemainingMs,
                BoostCooldownRemainingMs = player.BoostCooldownRemainingMs,
                SpeedMultiplier = player.SpeedMultiplier,
            };

            foreach (Snake.Cell cell in player.Body)
            {
                playerSnapshot.Body.Add(new Cell { X = cell.X, Y = cell.Y });
            }

            snapshot.Players.Add(playerSnapshot);
        }

        return snapshot;
    }
}

public sealed partial class PlayerSnapshot : IPlayerView
{
    private IReadOnlyList<Snake.Cell>? _bodyView;

    IReadOnlyList<Snake.Cell> IPlayerView.Body =>
        _bodyView ??= Body.Select(cell => new Snake.Cell(cell.X, cell.Y)).ToArray();

    int IPlayerView.Length => Body.Count;
}

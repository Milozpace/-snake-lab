namespace Snake;

/// <summary>
/// Read-only shape required by the renderer. Implemented by the live
/// <see cref="GameWorld"/> and by snapshots received from the MQTT host.
/// </summary>
public interface IGameView
{
    Difficulty Difficulty { get; }

    Cell? Food { get; }

    IReadOnlyList<IPlayerView> Players { get; }

    int WinnerIndex { get; }
}

public interface IPlayerView
{
    int Index { get; }

    IReadOnlyList<Cell> Body { get; }

    int Score { get; }

    int Level { get; }

    int Length { get; }

    bool Alive { get; }

    bool IsBoostActive { get; }

    int BoostActiveRemainingMs { get; }

    int BoostCooldownRemainingMs { get; }

    double SpeedMultiplier { get; }
}

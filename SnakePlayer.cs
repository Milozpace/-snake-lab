namespace Snake;

/// <summary>
/// One player-controlled snake. Movement cadence, leveling, and the active
/// boost skill are all owned by the player; collision and board state live in
/// <see cref="GameWorld"/>.
/// </summary>
public sealed class SnakePlayer : IPlayerView
{
    public const int BoostDurationMs = 2_000;
    public const int BoostCooldownMs = 7_000;

    internal const int FoodsPerLevel = 4;

    private readonly List<Cell> _body = [];
    private readonly Queue<Direction> _pendingTurns = new();
    private readonly DifficultyProfile _profile;

    internal SnakePlayer(
        int index,
        IEnumerable<Cell> body,
        Direction direction,
        DifficultyProfile profile)
    {
        Index = index;
        Direction = direction;
        _profile = profile;
        _body.AddRange(body);

        if (_body.Count == 0)
        {
            throw new ArgumentException("A snake needs at least one body cell.", nameof(body));
        }
    }

    public int Index { get; }

    public IReadOnlyList<Cell> Body => _body;

    public Cell Head => _body[0];

    public Direction Direction { get; private set; }

    public int Score { get; private set; }

    public int FoodsEaten { get; private set; }

    public int Level => 1 + FoodsEaten / FoodsPerLevel;

    public int Length => _body.Count;

    public bool Alive { get; internal set; } = true;

    /// <summary>Remaining milliseconds until this player's next requested move.</summary>
    public int MoveCooldownMs { get; internal set; }

    public int BoostActiveRemainingMs { get; private set; }

    public int BoostCooldownRemainingMs { get; private set; }

    public bool IsBoostActive => BoostActiveRemainingMs > 0;

    public bool IsBoostReady =>
        Alive && !IsBoostActive && BoostCooldownRemainingMs <= 0;

    public int BaseMoveIntervalMs => _profile.TickIntervalForLevel(Level);

    public int BoostedMoveIntervalMs =>
        Math.Max(35, (int)Math.Round(BaseMoveIntervalMs * 0.55));

    public int CurrentMoveIntervalMs =>
        IsBoostActive ? BoostedMoveIntervalMs : BaseMoveIntervalMs;

    public double SpeedMultiplier => _profile.BaseTickMs / (double)CurrentMoveIntervalMs;

    internal bool TryTurn(Direction direction)
    {
        if (!Alive || _pendingTurns.Count >= 2)
        {
            return false;
        }

        Direction reference = _pendingTurns.Count > 0 ? _pendingTurns.Last() : Direction;
        if (direction == reference || direction.IsOpposite(reference))
        {
            return false;
        }

        _pendingTurns.Enqueue(direction);
        return true;
    }

    internal void ClearPendingTurns() => _pendingTurns.Clear();

    internal void ApplyQueuedTurn()
    {
        if (_pendingTurns.Count == 0)
        {
            return;
        }

        Direction next = _pendingTurns.Dequeue();
        if (!next.IsOpposite(Direction))
        {
            Direction = next;
        }
    }

    internal bool ActivateBoost()
    {
        if (!IsBoostReady)
        {
            return false;
        }

        BoostActiveRemainingMs = BoostDurationMs;
        BoostCooldownRemainingMs = BoostCooldownMs;

        // Let the boost affect the movement that is already being waited on.
        MoveCooldownMs = Math.Max(0, Math.Min(MoveCooldownMs, CurrentMoveIntervalMs));
        return true;
    }

    internal void TickTimers(int deltaMs)
    {
        if (BoostActiveRemainingMs > 0)
        {
            BoostActiveRemainingMs = Math.Max(0, BoostActiveRemainingMs - deltaMs);
        }

        if (BoostCooldownRemainingMs > 0)
        {
            BoostCooldownRemainingMs = Math.Max(0, BoostCooldownRemainingMs - deltaMs);
        }
    }

    internal void TickMoveCooldown(int deltaMs) => MoveCooldownMs -= deltaMs;

    internal void CommitMove(Cell newHead, bool ateFood, int pointsPerFood)
    {
        _body.Insert(0, newHead);

        if (ateFood)
        {
            Score += pointsPerFood;
            FoodsEaten++;
        }
        else
        {
            _body.RemoveAt(_body.Count - 1);
        }
    }

    internal void MarkDead()
    {
        Alive = false;
        _pendingTurns.Clear();
        BoostActiveRemainingMs = 0;
    }
}

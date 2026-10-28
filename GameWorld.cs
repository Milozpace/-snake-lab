namespace Snake;

/// <summary>
/// Shared two-player arena. All players advance on the same fixed simulation
/// step, but each owns an independent movement cooldown, so their effective
/// speeds can differ because of leveling and the boost skill.
/// </summary>
public sealed class GameWorld : IGameView
{
    public const int Width = 36;
    public const int Height = 16;
    public const int PointsPerFood = 10;

    private readonly Random _random;
    private readonly DifficultyProfile _profile;
    private readonly SnakePlayer[] _players;
    private int _foodEatenCounter;

    public GameWorld(Difficulty difficulty, Random? random = null)
    {
        Difficulty = difficulty;
        _profile = DifficultyProfile.For(difficulty);
        _random = random ?? Random.Shared;

        _players =
        [
            new SnakePlayer(
                0,
                [new Cell(8, Height / 2), new Cell(7, Height / 2), new Cell(6, Height / 2)],
                Direction.Right,
                _profile),
            new SnakePlayer(
                1,
                [new Cell(27, Height / 2), new Cell(28, Height / 2), new Cell(29, Height / 2)],
                Direction.Left,
                _profile),
        ];

        foreach (SnakePlayer player in _players)
        {
            // Both snakes start after one normal movement interval. This gives
            // the players a moment to orient before the round begins.
            player.MoveCooldownMs = player.BaseMoveIntervalMs;
        }

        if (!SpawnFood())
        {
            IsOver = true;
            WinnerIndex = -1;
        }
    }

    public Difficulty Difficulty { get; }

    public IReadOnlyList<SnakePlayer> Players => _players;

    IReadOnlyList<IPlayerView> IGameView.Players => _players;

    public Cell? Food { get; private set; }

    public bool IsOver { get; private set; }

    /// <summary>0 = player 1 wins, 1 = player 2 wins, -1 = draw.</summary>
    public int WinnerIndex { get; private set; } = -1;

    public long GameTimeMs { get; private set; }

    public int FoodEatenCount => _foodEatenCounter;

    public bool HasVisibleSkillTimers =>
        _players.Any(player => player.Alive
            && (player.IsBoostActive || player.BoostCooldownRemainingMs > 0));

    public bool TryTurn(int playerIndex, Direction direction)
    {
        if (IsOver)
        {
            return false;
        }

        return GetPlayer(playerIndex).TryTurn(direction);
    }

    public bool TryActivateBoost(int playerIndex)
    {
        if (IsOver)
        {
            return false;
        }

        return GetPlayer(playerIndex).ActivateBoost();
    }

    public void ClearPendingTurns()
    {
        foreach (SnakePlayer player in _players)
        {
            player.ClearPendingTurns();
        }
    }

    /// <summary>Advances the world by one fixed simulation step.</summary>
    /// <returns>True when a snake moved or the board changed.</returns>
    public bool Update(int deltaMs)
    {
        if (IsOver || deltaMs <= 0)
        {
            return false;
        }

        GameTimeMs += deltaMs;

        foreach (SnakePlayer player in _players)
        {
            if (player.Alive)
            {
                player.TickTimers(deltaMs);
            }
        }

        var movers = new List<SnakePlayer>(2);

        foreach (SnakePlayer player in _players)
        {
            if (!player.Alive)
            {
                continue;
            }

            player.TickMoveCooldown(deltaMs);
            if (player.MoveCooldownMs <= 0)
            {
                movers.Add(player);
            }
        }

        if (movers.Count == 0)
        {
            return false;
        }

        ResolveMoves(movers);
        return true;
    }

    internal void SetFoodForTesting(Cell food)
    {
        if (food.X < 0 || food.X >= Width || food.Y < 0 || food.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(food), "Food must be inside the board.");
        }

        if (_players.Any(player => player.Body.Contains(food)))
        {
            throw new ArgumentException("Food cannot be placed on a snake.", nameof(food));
        }

        Food = food;
    }

    private SnakePlayer GetPlayer(int index) =>
        index is >= 0 and < 2
            ? _players[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    private void ResolveMoves(List<SnakePlayer> movers)
    {
        var plansByPlayer = new MovePlan?[_players.Length];

        foreach (SnakePlayer player in movers)
        {
            player.ApplyQueuedTurn();
            Cell newHead = player.Head + player.Direction;
            bool hitsWall = newHead.X < 0
                || newHead.X >= Width
                || newHead.Y < 0
                || newHead.Y >= Height;
            bool eatsFood = !hitsWall && Food is { } food && food == newHead;

            var plan = new MovePlan(player, newHead, hitsWall, eatsFood);
            plansByPlayer[player.Index] = plan;
        }

        // Build the body each snake would occupy after this step. Using the
        // post-move bodies makes tail-vacating and head-swap rules symmetric.
        var afterBodies = new List<Cell>?[_players.Length];

        foreach (SnakePlayer player in _players)
        {
            if (!player.Alive)
            {
                afterBodies[player.Index] = null;
                continue;
            }

            MovePlan? plan = plansByPlayer[player.Index];
            if (plan is null || plan.HitsWall)
            {
                afterBodies[player.Index] = player.Body.ToList();
                continue;
            }

            var body = new List<Cell>(player.Length + 1) { plan.NewHead };
            body.AddRange(player.Body);
            if (!plan.EatsFood)
            {
                body.RemoveAt(body.Count - 1);
            }

            afterBodies[player.Index] = body;
        }

        // Evaluate every intended move against the full post-move bodies.
        // A snake may not enter its own body or any other snake's body.
        foreach (MovePlan? plan in plansByPlayer)
        {
            if (plan is null)
            {
                continue;
            }

            if (plan.HitsWall)
            {
                plan.Player.MarkDead();
                continue;
            }

            foreach (SnakePlayer other in _players)
            {
                List<Cell>? body = afterBodies[other.Index];
                if (body is null)
                {
                    continue;
                }

                int start = ReferenceEquals(other, plan.Player) ? 1 : 0;
                for (int i = start; i < body.Count; i++)
                {
                    if (body[i] == plan.NewHead)
                    {
                        plan.Player.MarkDead();
                        break;
                    }
                }

                if (!plan.Player.Alive)
                {
                    break;
                }
            }
        }

        bool foodEaten = false;

        foreach (MovePlan? plan in plansByPlayer)
        {
            if (plan is null || !plan.Player.Alive)
            {
                continue;
            }

            bool ateFood = plan.EatsFood && Food is { } food && food == plan.NewHead;
            plan.Player.CommitMove(plan.NewHead, ateFood, PointsPerFood);

            if (ateFood)
            {
                foodEaten = true;
                Food = null;
                _foodEatenCounter++;
            }
        }

        int deadCount = _players.Count(player => !player.Alive);

        if (deadCount > 0)
        {
            IsOver = true;
            if (deadCount == 1)
            {
                int loser = Array.FindIndex(_players, player => !player.Alive);
                WinnerIndex = 1 - loser;
            }
            else
            {
                WinnerIndex = -1;
            }
        }
        else
        {
            foreach (SnakePlayer player in movers)
            {
                player.MoveCooldownMs += player.CurrentMoveIntervalMs;
            }

            if (foodEaten && !SpawnFood())
            {
                IsOver = true;
                WinnerIndex = _players[0].Score == _players[1].Score
                    ? -1
                    : (_players[0].Score > _players[1].Score ? 0 : 1);
            }
        }
    }

    private bool SpawnFood()
    {
        var occupied = new HashSet<Cell>();
        foreach (SnakePlayer player in _players)
        {
            occupied.UnionWith(player.Body);
        }

        if (occupied.Count >= Width * Height)
        {
            Food = null;
            return false;
        }

        for (int attempt = 0; attempt < 512; attempt++)
        {
            var candidate = new Cell(_random.Next(Width), _random.Next(Height));
            if (!occupied.Contains(candidate))
            {
                Food = candidate;
                return true;
            }
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var candidate = new Cell(x, y);
                if (!occupied.Contains(candidate))
                {
                    Food = candidate;
                    return true;
                }
            }
        }

        Food = null;
        return false;
    }

    private sealed class MovePlan(
        SnakePlayer player,
        Cell newHead,
        bool hitsWall,
        bool eatsFood)
    {
        public SnakePlayer Player { get; } = player;

        public Cell NewHead { get; } = newHead;

        public bool HitsWall { get; } = hitsWall;

        public bool EatsFood { get; } = eatsFood;
    }
}

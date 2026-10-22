namespace Snake;

public sealed class SnakeGame
{
    public const int Width = 36;
    public const int Height = 16;
    public const int PointsPerFood = 10;

    private const int FoodsPerLevel = 4;
    private const int MaxQueuedTurns = 2;

    private readonly Random _random;
    private readonly List<Cell> _snake = [];
    private readonly HashSet<Cell> _occupied = [];
    private readonly Queue<Direction> _pendingTurns = new();
    private readonly DifficultyProfile _profile;

    private Direction _direction = Direction.Right;
    private Cell? _food;

    public SnakeGame(Difficulty difficulty, Random? random = null)
    {
        Difficulty = difficulty;
        _profile = DifficultyProfile.For(difficulty);
        _random = random ?? Random.Shared;

        var start = new Cell(Width / 2, Height / 2);
        _snake.Add(start);
        _snake.Add(start with { X = start.X - 1 });
        _snake.Add(start with { X = start.X - 2 });
        _occupied.UnionWith(_snake);

        SpawnFood();
    }

    public Difficulty Difficulty { get; }

    public IReadOnlyList<Cell> Snake => _snake;

    public Cell? Food => _food;

    public Direction CurrentDirection => _direction;

    public int Score { get; private set; }

    public int FoodsEaten { get; private set; }

    public int Level => 1 + FoodsEaten / FoodsPerLevel;

    public int Length => _snake.Count;

    public bool IsGameOver { get; private set; }

    public bool IsWon { get; private set; }

    public int TickIntervalMs => _profile.TickIntervalForLevel(Level);

    public double SpeedMultiplier => _profile.BaseTickMs / (double)TickIntervalMs;

    /// <summary>
    /// Queues a turn. Turns are applied one per game tick, so rapid key presses
    /// can never fold the snake back into itself.
    /// </summary>
    public bool TryTurn(Direction direction)
    {
        if (IsGameOver || IsWon || _pendingTurns.Count >= MaxQueuedTurns)
        {
            return false;
        }

        Direction reference = _pendingTurns.Count > 0 ? _pendingTurns.Last() : _direction;
        if (direction == reference || direction.IsOpposite(reference))
        {
            return false;
        }

        _pendingTurns.Enqueue(direction);
        return true;
    }

    public void ClearPendingTurns() => _pendingTurns.Clear();

    public void Advance()
    {
        if (IsGameOver || IsWon)
        {
            return;
        }

        if (_pendingTurns.Count > 0)
        {
            Direction next = _pendingTurns.Dequeue();
            if (!next.IsOpposite(_direction))
            {
                _direction = next;
            }
        }

        Cell newHead = _snake[0] + _direction;
        if (newHead.X < 0 || newHead.X >= Width || newHead.Y < 0 || newHead.Y >= Height)
        {
            IsGameOver = true;
            return;
        }

        bool eating = _food is { } food && food == newHead;

        // When the snake is not eating, its tail moves away this tick, so the
        // new head is allowed to occupy the tail's old cell.
        int collisionCheckCount = eating ? _snake.Count : _snake.Count - 1;
        for (int i = 0; i < collisionCheckCount; i++)
        {
            if (_snake[i] == newHead)
            {
                IsGameOver = true;
                return;
            }
        }

        _snake.Insert(0, newHead);
        _occupied.Add(newHead);

        if (eating)
        {
            _food = null;
            Score += PointsPerFood;
            FoodsEaten++;
            SpawnFood();
        }
        else
        {
            int tailIndex = _snake.Count - 1;
            _occupied.Remove(_snake[tailIndex]);
            _snake.RemoveAt(tailIndex);
        }
    }

    /// <summary>Used by the built-in self-test to make food mechanics deterministic.</summary>
    internal void SetFoodForTesting(Cell food)
    {
        if (food.X < 0 || food.X >= Width || food.Y < 0 || food.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(food), "Food must be inside the board.");
        }

        if (_occupied.Contains(food))
        {
            throw new ArgumentException("Food cannot be placed on the snake.", nameof(food));
        }

        _food = food;
        IsWon = false;
    }

    private void SpawnFood()
    {
        if (_occupied.Count >= Width * Height)
        {
            _food = null;
            IsWon = true;
            return;
        }

        for (int attempt = 0; attempt < 512; attempt++)
        {
            var candidate = new Cell(_random.Next(Width), _random.Next(Height));
            if (!_occupied.Contains(candidate))
            {
                _food = candidate;
                return;
            }
        }

        // The random attempts can get unlucky on a crowded board. Fall back to
        // a deterministic scan to guarantee a free cell is found.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var candidate = new Cell(x, y);
                if (!_occupied.Contains(candidate))
                {
                    _food = candidate;
                    return;
                }
            }
        }

        _food = null;
        IsWon = true;
    }
}

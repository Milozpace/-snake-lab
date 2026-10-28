using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snake;

public static class NetworkMessageTypes
{
    public const string Hello = "hello";
    public const string Input = "input";
    public const string Rematch = "rematch";
    public const string State = "state";
}

public sealed class NetworkEnvelope
{
    public int Protocol { get; set; } = NetworkProtocol.Version;

    public string Type { get; set; } = string.Empty;

    public string? Room { get; set; }

    public Direction? Direction { get; set; }

    public bool Boost { get; set; }

    public OnlineGameSnapshot? State { get; set; }
}

public static class NetworkProtocol
{
    public const int Version = 1;

    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string HostTopic(string room) => $"snake/v3/{room}/host";

    public static string GuestTopic(string room) => $"snake/v3/{room}/guest";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// Complete visual state broadcast by the MQTT host. The guest never simulates
/// the world; it only renders these snapshots and sends input back.
/// </summary>
public sealed class OnlineGameSnapshot : IGameView
{
    public long Sequence { get; set; }

    public long GameTimeMs { get; set; }

    public Difficulty Difficulty { get; set; }

    public Cell? Food { get; set; }

    public bool IsOver { get; set; }

    public int WinnerIndex { get; set; } = -1;

    public List<OnlinePlayerSnapshot> PlayerStates { get; set; } = [];

    IReadOnlyList<IPlayerView> IGameView.Players => PlayerStates;

    public static OnlineGameSnapshot Capture(GameWorld world, long sequence)
    {
        var snapshot = new OnlineGameSnapshot
        {
            Sequence = sequence,
            GameTimeMs = world.GameTimeMs,
            Difficulty = world.Difficulty,
            Food = world.Food,
            IsOver = world.IsOver,
            WinnerIndex = world.WinnerIndex,
        };

        foreach (SnakePlayer player in world.Players)
        {
            snapshot.PlayerStates.Add(new OnlinePlayerSnapshot
            {
                Index = player.Index,
                Body = player.Body.ToList(),
                Score = player.Score,
                Level = player.Level,
                Alive = player.Alive,
                IsBoostActive = player.IsBoostActive,
                BoostActiveRemainingMs = player.BoostActiveRemainingMs,
                BoostCooldownRemainingMs = player.BoostCooldownRemainingMs,
                SpeedMultiplier = player.SpeedMultiplier,
            });
        }

        return snapshot;
    }
}

public sealed class OnlinePlayerSnapshot : IPlayerView
{
    public int Index { get; set; }

    public List<Cell> Body { get; set; } = [];

    public int Score { get; set; }

    public int Level { get; set; }

    public bool Alive { get; set; }

    public bool IsBoostActive { get; set; }

    public int BoostActiveRemainingMs { get; set; }

    public int BoostCooldownRemainingMs { get; set; }

    public double SpeedMultiplier { get; set; }

    IReadOnlyList<Cell> IPlayerView.Body => Body;

    public int Length => Body.Count;
}

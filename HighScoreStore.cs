using System.Globalization;
using System.Text;

namespace Snake;

public sealed class HighScoreStore
{
    private readonly string _path;
    private readonly Dictionary<Difficulty, int> _bestScores = [];

    public HighScoreStore(string? path = null)
    {
        _path = path ?? GetDefaultPath();
        Load();
    }

    public string Path => _path;

    public int GetBest(Difficulty difficulty) =>
        _bestScores.TryGetValue(difficulty, out int score) ? score : 0;

    /// <summary>Stores a score if it beats the current best. Returns true on a new record.</summary>
    public bool UpdateBest(Difficulty difficulty, int score)
    {
        if (score <= 0 || score <= GetBest(difficulty))
        {
            return false;
        }

        _bestScores[difficulty] = score;
        TrySave();
        return true;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            foreach (string rawLine in File.ReadLines(_path))
            {
                string line = rawLine.Trim();
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int score) || score < 0)
                {
                    continue;
                }

                Difficulty? difficulty = key.ToLowerInvariant() switch
                {
                    "chill" => Difficulty.Chill,
                    "classic" => Difficulty.Classic,
                    "turbo" => Difficulty.Turbo,
                    _ => null,
                };

                if (difficulty is not null)
                {
                    _bestScores[difficulty.Value] = score;
                }
            }
        }
        catch (IOException)
        {
            // A missing or unreadable score file must never prevent the game from starting.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void TrySave()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            foreach (Difficulty difficulty in DifficultyProfile.All)
            {
                builder.Append(difficulty.ToString().ToLowerInvariant())
                    .Append('=')
                    .Append(GetBest(difficulty).ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            File.WriteAllText(_path, builder.ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetDefaultPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("SNAKE_SCORE_FILE");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return System.IO.Path.Combine(baseDirectory, "snake-terminal", "highscores.txt");
    }
}

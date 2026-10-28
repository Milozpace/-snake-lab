namespace Snake;

public enum OnlineRole
{
    Host,
    Guest,
}

public sealed record OnlineOptions(
    OnlineRole Role,
    string Room,
    string BrokerHost,
    int BrokerPort,
    Difficulty Difficulty)
{
    public string BrokerDisplay => $"{BrokerHost}:{BrokerPort}";
}

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Snake;

/// <summary>
/// Thin MQTT wrapper used by both host and guest. Messages are serialized as
/// JSON envelopes and delivered to the game loop through a concurrent queue.
/// </summary>
public sealed class MqttTransport : IDisposable
{
    private readonly IMqttClient _client;
    private readonly ConcurrentQueue<NetworkEnvelope> _inbox = new();
    private readonly JsonSerializerOptions _json = NetworkProtocol.JsonOptions;
    private bool _disposed;

    private MqttTransport(IMqttClient client)
    {
        _client = client;
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;
    }

    public bool IsConnected => _client.IsConnected;

    public static MqttTransport Connect(
        string host,
        int port,
        string clientId,
        string subscribeTopic,
        CancellationToken cancellationToken)
    {
        var factory = new MqttFactory();
        IMqttClient client = factory.CreateMqttClient();
        var transport = new MqttTransport(client);

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCleanSession()
            .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();

        try
        {
            var connectResult = client.ConnectAsync(options, cancellationToken).GetAwaiter().GetResult();
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new InvalidOperationException(
                    $"MQTT connection failed: {connectResult.ResultCode}.");
            }

            var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(subscribeTopic)
                .Build();

            client.SubscribeAsync(subscribeOptions, cancellationToken).GetAwaiter().GetResult();
            return transport;
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    public void Publish(string topic, NetworkEnvelope envelope)
    {
        if (_disposed || !_client.IsConnected)
        {
            return;
        }

        string payload = JsonSerializer.Serialize(envelope, _json);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        // State snapshots are small and periodic. Fire-and-forget keeps the
        // 5 ms game loop from waiting on the broker; sequence numbers let the
        // guest ignore an out-of-order snapshot if one ever arrives.
        _ = _client.PublishAsync(message).ContinueWith(
            task => { _ = task.Exception; },
            TaskScheduler.Default);
    }

    public bool TryDequeue(out NetworkEnvelope envelope) => _inbox.TryDequeue(out envelope!);

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            string payload = args.ApplicationMessage.ConvertPayloadToString();
            NetworkEnvelope? message = JsonSerializer.Deserialize<NetworkEnvelope>(payload, _json);
            if (message is not null && message.Protocol == NetworkProtocol.Version)
            {
                _inbox.Enqueue(message);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed traffic on the shared broker.
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.ApplicationMessageReceivedAsync -= OnMessageReceived;

        try
        {
            if (_client.IsConnected)
            {
                _client.DisconnectAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Best-effort disconnect while leaving the alternate screen.
        }

        _client.Dispose();
    }
}

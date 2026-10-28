using System.Collections.Concurrent;
using Google.Protobuf;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Snake.Protocol;

namespace Snake;

/// <summary>
/// Thin MQTT 5.2 wrapper used by both host and guest. Protobuf envelopes are
/// delivered to the game loop through a concurrent queue.
/// </summary>
public sealed class MqttTransport : IDisposable
{
    private readonly IMqttClient _client;
    private readonly ConcurrentQueue<Envelope> _inbox = new();
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
        var factory = new MqttClientFactory();
        IMqttClient client = factory.CreateMqttClient();
        var transport = new MqttTransport(client);

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithProtocolVersion(MqttProtocolVersion.V500)
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
                .WithTopicFilter(f =>
                    f.WithTopic(subscribeTopic).WithAtMostOnceQoS())
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

    public void Publish(string topic, Envelope envelope)
    {
        if (_disposed || !_client.IsConnected)
        {
            return;
        }

        envelope.Protocol = NetworkProtocol.Version;
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(envelope.ToByteArray())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        // State snapshots are small and periodic. Fire-and-forget keeps the
        // 5 ms game loop from waiting on the broker; sequence numbers let the
        // guest ignore an out-of-order snapshot if one ever arrives.
        _ = _client.PublishAsync(message).ContinueWith(
            task => { _ = task.Exception; },
            TaskScheduler.Default);
    }

    public bool TryDequeue(out Envelope envelope) => _inbox.TryDequeue(out envelope!);

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            Envelope envelope = Envelope.Parser.ParseFrom(args.ApplicationMessage.Payload);
            if (envelope.Protocol == NetworkProtocol.Version)
            {
                _inbox.Enqueue(envelope);
            }
        }
        catch (InvalidProtocolBufferException)
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

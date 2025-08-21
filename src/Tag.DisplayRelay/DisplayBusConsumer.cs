using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Tag.Contracts;

namespace Tag.DisplayRelay;

public class DisplayBusConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly GroupHub _hub;
    private readonly RabbitOptions _opt;
    private readonly ILogger<DisplayBusConsumer> _log;

    public DisplayBusConsumer(GroupHub hub, RabbitOptions opt, ILogger<DisplayBusConsumer> log)
    { _hub = hub; _opt = opt; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_opt.Uri) };

        await using var conn = await factory.CreateConnectionAsync(cancellationToken: ct);
        await using var ch   = await conn.CreateChannelAsync(cancellationToken: ct);

        await ch.ExchangeDeclareAsync("enlistment.events", type: "topic", durable: true, autoDelete: false, arguments: null, cancellationToken: ct);

        // Ephemeral queue for this instance
        var qName = $"display-relay.{Guid.NewGuid():N}";
        var q = await ch.QueueDeclareAsync(queue: qName, durable: false, exclusive: false, autoDelete: true, arguments: null, cancellationToken: ct);

        await ch.QueueBindAsync(q.QueueName, "enlistment.events", $"site.*.room.*.{EventNames.DisplayAppend}", cancellationToken: ct);
        await ch.QueueBindAsync(q.QueueName, "enlistment.events", $"site.*.room.*.{EventNames.DisplayRemove}", cancellationToken: ct);

        await ch.BasicQosAsync(prefetchSize: 0, prefetchCount: 100, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var env  = JsonSerializer.Deserialize<Envelope>(json, JsonOpts)!;

                var group = $"{env.SiteId}:{env.RoomId}";

                switch (env.Name)
                {
                    case EventNames.DisplayAppend:
                    {
                        var p = ((JsonElement)env.Payload!).Deserialize<DisplayAppendPayload>(JsonOpts)!;
                        await _hub.BroadcastAsync(group, new {
                            type = "append",
                            visitSessionId = p.VisitSessionId,
                            enlisteeName   = p.EnlisteeName,
                            packLocation   = p.PackLocation
                        });
                        break;
                    }
                    case EventNames.DisplayRemove:
                    {
                        var p = ((JsonElement)env.Payload!).Deserialize<DisplayRemovePayload>(JsonOpts)!;
                        await _hub.BroadcastAsync(group, new {
                            type = "remove",
                            visitSessionId = p.VisitSessionId
                        });
                        break;
                    }
                }

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Display relay failed; NACK & requeue");
                await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
            }

            return;
        };

        await ch.BasicConsumeAsync(queue: q.QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);

        _log.LogInformation("Display relay listening on {Queue}", qName);

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { /* graceful shutdown */ }
    }
}

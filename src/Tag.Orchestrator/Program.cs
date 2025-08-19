using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Tag.Contracts;

await Host.CreateDefaultBuilder(args)
  .ConfigureServices((ctx, services) =>
  {
      services.AddSingleton(new RabbitOptions
      {
          Uri = ctx.Configuration["Rabbit:Uri"] ?? "amqp://guest:guest@localhost:5672"
      });
      services.AddHostedService<OrchestratorWorker>();
  })
  .ConfigureLogging(b => b.AddConsole())
  .RunConsoleAsync();

internal sealed record RabbitOptions { public string Uri { get; init; } = default!; }

internal sealed class OrchestratorWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<OrchestratorWorker> _log;
    private readonly RabbitOptions _opt;

    public OrchestratorWorker(ILogger<OrchestratorWorker> log, RabbitOptions opt)
    { _log = log; _opt = opt; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_opt.Uri) };

        await using var conn = await factory.CreateConnectionAsync(cancellationToken: ct);
        await using var ch   = await conn.CreateChannelAsync(cancellationToken: ct);

        // Exchanges (idempotent)
        await ch.ExchangeDeclareAsync("enlistment.events",  type: "topic", durable: true, autoDelete: false, arguments: null, cancellationToken: ct);
        await ch.ExchangeDeclareAsync("enlistment.commands",type: "topic", durable: true, autoDelete: false, arguments: null, cancellationToken: ct);

        // Orchestrator queue + bindings
        var q = await ch.QueueDeclareAsync("orchestrator.q", durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);
        await ch.QueueBindAsync(q.QueueName, "enlistment.events", "site.*.room.*." + EventNames.EntryScanAccepted, cancellationToken: ct);
        await ch.QueueBindAsync(q.QueueName, "enlistment.events", "site.*.room.*." + EventNames.ExitScanAccepted,  cancellationToken: ct);

        // Prefetch
        await ch.BasicQosAsync(prefetchSize: 0, prefetchCount: 32, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(ch);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var env  = JsonSerializer.Deserialize<Envelope>(json, JsonOpts)!;

                var site = env.SiteId;
                var room = env.RoomId;
                var vsId = env.VisitSessionId;

                // helper: publish follow-ups
                async Task PublishAsync(string evtName, object payloadObj)
                {
                    var outEnv = new
                    {
                        messageId = Guid.NewGuid(),
                        name = evtName,
                        siteId = site,
                        roomId = room,
                        visitSessionId = vsId,
                        payload = payloadObj
                    };

                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outEnv));
                    var props = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };
                    var rk    = $"site.{site}.room.{room}.{evtName}";

                    await ch.BasicPublishAsync(
                        exchange: "enlistment.events",
                        routingKey: rk,
                        mandatory: false,
                        basicProperties: props,
                        body: bytes,
                        cancellationToken: CancellationToken.None);
                }

                switch (env.Name)
                {
                    case EventNames.EntryScanAccepted:
                    {
                        var entry = ((JsonElement)env.Payload!).Deserialize<EntryScanAcceptedPayload>(JsonOpts)!;

                        await PublishAsync(EventNames.DoorOpenRequested, new { reason = "entry" });
                        await PublishAsync(EventNames.EntryGranted,      new { visitSessionId = vsId, packLocation = entry.PackLocation });
                        await PublishAsync(EventNames.DisplayAppend,     new { visitSessionId = vsId, enlisteeName = entry.EnlisteeName, packLocation = entry.PackLocation });
                        break;
                    }
                    case EventNames.ExitScanAccepted:
                    {
                        await PublishAsync(EventNames.DisplayRemove, new { visitSessionId = vsId });
                        break;
                    }
                    // (ignore others in PoC)
                }

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error handling message; NACK & requeue");
                await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
            }

            return;
        };

        // Start consuming (manual acks)
        await ch.BasicConsumeAsync(
            queue: q.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _log.LogInformation("Orchestrator listening on {Queue}", q.QueueName);

        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { /* graceful shutdown */ }
    }
}


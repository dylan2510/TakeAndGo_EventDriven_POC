using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Tag.Contracts;
using Tag.Infrastructure;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var cs = ctx.Configuration["ConnectionStrings:MainDb"]
                 ?? "Host=localhost;Username=tag;Password=tagpw;Database=tagdb";
        services.AddTagDb(cs); // from infrastructure

        services.AddSingleton(new RabbitOptions
        {
            Uri = ctx.Configuration["Rabbit:Uri"] ?? "amqp://guest:guest@localhost:5672"
        });

        services.AddSingleton(new OutboxOptions
        {
            BatchSize   = int.TryParse(ctx.Configuration["Outbox:BatchSize"], out var b) ? b : 200,
            PollDelayMs = int.TryParse(ctx.Configuration["Outbox:PollDelayMs"], out var d) ? d : 500
        });

        services.AddHostedService<OutboxPublisher>();
    })
    .ConfigureLogging(b => b.AddConsole())
    .RunConsoleAsync();

internal sealed record RabbitOptions
{
    public string Uri { get; init; } = default!;
}

internal sealed record OutboxOptions
{
    public int BatchSize { get; init; } = 200;
    public int PollDelayMs { get; init; } = 500;
}

internal sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxPublisher> _log;
    private readonly RabbitOptions _rabbit;
    private readonly OutboxOptions _opt;

    public OutboxPublisher(IServiceProvider sp, ILogger<OutboxPublisher> log, RabbitOptions rabbit, OutboxOptions opt)
    {
        _sp = sp; _log = log; _rabbit = rabbit; _opt = opt;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(_rabbit.Uri) };

        // v7.x async connect/channel (note the named args for the CancellationToken)
        await using var conn = await factory.CreateConnectionAsync(cancellationToken: ct);
        await using var ch   = await conn.CreateChannelAsync(cancellationToken: ct);

        // Declare exchanges (idempotent)
        await ch.ExchangeDeclareAsync(
            exchange: "enlistment.events",
            type: "topic",
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await ch.ExchangeDeclareAsync(
            exchange: "enlistment.commands",
            type: "topic",
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        _log.LogInformation("OutboxPublisher started. RabbitMQ: {Uri}", _rabbit.Uri);

        var pollDelay = TimeSpan.FromMilliseconds(_opt.PollDelayMs);

        while (!ct.IsCancellationRequested)
        {
            int published = 0;

            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TagDbContext>();

                var batch = await db.Outbox
                    .Where(o => o.PublishedAt == null)
                    .OrderBy(o => o.OutboxId)
                    .Take(_opt.BatchSize)
                    .ToListAsync(ct);

                foreach (var o in batch)
                {
                    var env = new Envelope(
                        MessageId: o.MessageId,
                        Name: o.Name,
                        SiteId: o.SiteId,
                        RoomId: o.RoomId,
                        VisitSessionId: o.VisitSessionId,
                        Payload: JsonSerializer.Deserialize<object>(o.Payload)
                    );

                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(env));
                    var props = new BasicProperties
                    {
                        DeliveryMode = DeliveryModes.Persistent
                    };

                    var routingKey = $"site.{o.SiteId}.room.{o.RoomId}.{o.Name}";

                    await ch.BasicPublishAsync(
                        exchange: "enlistment.events",
                        routingKey: routingKey,
                        mandatory: false,
                        basicProperties: props,
                        body: new ReadOnlyMemory<byte>(bytes),
                        cancellationToken: ct);

                    o.PublishedAt = DateTime.UtcNow;
                    published++;
                }

                if (published > 0)
                    await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error while publishing outbox.");
                // brief backoff on error so we don't spin
                await Task.Delay(1000, ct);
            }

            if (published == 0)
                await Task.Delay(pollDelay, ct);
        }
    }
}

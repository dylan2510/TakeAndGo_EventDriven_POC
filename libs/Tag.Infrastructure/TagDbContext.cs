using Microsoft.EntityFrameworkCore;
using Tag.Domain;

namespace Tag.Infrastructure;

public class TagDbContext : DbContext
{
    public TagDbContext(DbContextOptions<TagDbContext> options) : base(options) { }

    public DbSet<VisitSession> VisitSessions => Set<VisitSession>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<VisitSession>(e =>
        {
            e.HasKey(x => x.VisitSessionId);
            e.Property(x => x.SiteId).IsRequired();
            e.Property(x => x.RoomId).IsRequired();
            e.Property(x => x.EnlisteeId).IsRequired();
            e.Property(x => x.EnlisteeName).IsRequired();
            e.Property(x => x.PackLocation).IsRequired();
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.OutboxId);
            e.HasIndex(x => x.MessageId).IsUnique();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.SiteId).IsRequired();
            e.Property(x => x.RoomId).IsRequired();
            e.Property(x => x.Payload).IsRequired();
        });
    }
}

public class OutboxMessage
{
    public long OutboxId { get; set; }                 // identity PK
    public Guid MessageId { get; set; }                // for de-dup
    public string Name { get; set; } = default!;       // event name
    public string SiteId { get; set; } = default!;
    public string RoomId { get; set; } = default!;
    public Guid VisitSessionId { get; set; }
    public string Payload { get; set; } = default!;    // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }         // null = unsent
    public int RetryCount { get; set; }
}

namespace Tag.Domain;
public enum VisitState { Pending, Active, Completed, Stale }
public class VisitSession
{
    public Guid VisitSessionId { get; set; }
    public string SiteId { get; set; } = default!;
    public string RoomId { get; set; } = default!;
    public string EnlisteeId { get; set; } = default!;
    public string EnlisteeName { get; set; } = default!;
    public string PackLocation { get; set; } = default!;
    public VisitState State { get; set; } = VisitState.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}

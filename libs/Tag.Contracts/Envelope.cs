public record Envelope(
    Guid MessageId,
    string Name,
    string SiteId,
    string RoomId,
    Guid VisitSessionId,
    object? Payload);
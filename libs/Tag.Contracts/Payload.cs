public record EntryScanAcceptedPayload(Guid VisitSessionId, string EnlisteeName, string PackLocation);
public record ExitScanAcceptedPayload(Guid VisitSessionId);
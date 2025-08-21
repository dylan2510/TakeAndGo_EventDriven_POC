public record EntryScanAcceptedPayload(Guid VisitSessionId, string EnlisteeName, string PackLocation);
public record ExitScanAcceptedPayload(Guid VisitSessionId);

public record DisplayAppendPayload(Guid VisitSessionId, string EnlisteeName, string PackLocation);
public record DisplayRemovePayload(Guid VisitSessionId);
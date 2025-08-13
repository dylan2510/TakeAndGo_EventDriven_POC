using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tag.Contracts;
using Tag.Domain;
using Tag.Infrastructure;

namespace Tag.Api.Controllers;

[ApiController]
[Route("")]
public class VisitsController : ControllerBase
{
    private readonly TagDbContext _db;
    public VisitsController(TagDbContext db) => _db = db;

    // POST /entry-scan
    [HttpPost("entry-scan")]
    public async Task<IActionResult> EntryScan([FromBody] EntryRequest req)
    {
        var vsId = req.VisitSessionId ?? Guid.NewGuid();

        var visit = await _db.VisitSessions.FindAsync(vsId);
        if (visit is null)
        {
            visit = new VisitSession
            {
                VisitSessionId = vsId,
                SiteId = req.SiteId,
                RoomId = req.RoomId,
                EnlisteeId = req.EnlisteeId,
                EnlisteeName = req.EnlisteeName,
                PackLocation = req.PackLocation,
                State = VisitState.Active,
                StartedAt = DateTime.UtcNow
            };
            _db.VisitSessions.Add(visit);
        }
        else
        {
            visit.EnlisteeName = req.EnlisteeName;
            visit.PackLocation = req.PackLocation;
            visit.State = VisitState.Active;
        }

        var payload = new EntryScanAcceptedPayload(vsId, req.EnlisteeName, req.PackLocation);
        _db.Outbox.Add(new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Name = EventNames.EntryScanAccepted,
            SiteId = req.SiteId,
            RoomId = req.RoomId,
            VisitSessionId = vsId,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Accepted($"/visits/{vsId}", new { visitSessionId = vsId });
    }

    // POST /exit-scan
    [HttpPost("exit-scan")]
    public async Task<IActionResult> ExitScan([FromBody] ExitRequest req)
    {
        var visit = await _db.VisitSessions
            .FirstOrDefaultAsync(v => v.VisitSessionId == req.VisitSessionId);

        if (visit is null)
            return NotFound(new { error = "VisitSession not found" });

        visit.State = VisitState.Completed;
        visit.EndedAt = DateTime.UtcNow;

        var payload = new ExitScanAcceptedPayload(req.VisitSessionId);
        _db.Outbox.Add(new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Name = EventNames.ExitScanAccepted,
            SiteId = visit.SiteId,
            RoomId = visit.RoomId,
            VisitSessionId = visit.VisitSessionId,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    public record EntryRequest(
        string SiteId, string RoomId,
        string EnlisteeId, string EnlisteeName,
        string PackLocation, Guid? VisitSessionId);

    public record ExitRequest(Guid VisitSessionId);
}

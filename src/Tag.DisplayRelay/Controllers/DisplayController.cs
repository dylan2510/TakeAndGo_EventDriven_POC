using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tag.Infrastructure;

namespace Tag.DisplayRelay.Controllers;

[ApiController]
[Route("display")]
public class DisplayController : ControllerBase
{
    private readonly TagDbContext _db;
    public DisplayController(TagDbContext db) => _db = db;

    [HttpGet("state")]
    public async Task<IActionResult> State([FromQuery] string siteId, [FromQuery] string roomId)
    {
        var rows = await _db.VisitSessions
            .Where(v => v.SiteId == siteId && v.RoomId == roomId &&
                        (v.State == Tag.Domain.VisitState.Pending || v.State == Tag.Domain.VisitState.Active))
            .Select(v => new {
                visitSessionId = v.VisitSessionId,
                enlisteeName   = v.EnlisteeName,
                packLocation   = v.PackLocation
            })
            .ToListAsync();

        return Ok(rows);
    }
}

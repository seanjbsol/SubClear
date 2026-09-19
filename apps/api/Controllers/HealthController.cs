using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Data;

namespace SubClear.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseOk = await _db.Database.CanConnectAsync(cancellationToken);
        return Ok(new
        {
            status = databaseOk ? "ok" : "degraded",
            service = "SubClear API",
            timeUtc = DateTimeOffset.UtcNow,
            database = databaseOk ? "ok" : "unavailable"
        });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Endpoint público de salud y estado operativo de la API (.NET 8 + Supabase PostgreSQL).
/// </summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;
    private readonly TenisAhoraDbContext _dbContext;

    public HealthController(TenisAhoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dbConnected = false;
        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbConnected = false;
        }

        return Ok(new
        {
            status = "ok",
            service = "tenis-ahora-api",
            version = "2.0.0-dotnet8",
            engine = "Supabase PostgreSQL 15+ / EF Core 8",
            databaseConnected = dbConnected,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds
        });
    }
}

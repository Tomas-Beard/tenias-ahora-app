using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TenisAhora.Application.DTOs;
using TenisAhora.Application.Interfaces;
using TenisAhora.Domain.Entities;
using TenisAhora.Domain.Exceptions;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Controlador REST para los Módulos 2 y 3 (IEEE SRS-830): Canchas (<see cref="Cancha"/>),
/// Horarios (<see cref="HorarioCancha"/>), Bloqueos de Mantenimiento (<see cref="BloqueoMantenimiento"/>),
/// Tipos de Cancha (<see cref="TipoCancha"/>), Stock e Inventario (<see cref="Producto"/> + <see cref="Stock"/>)
/// y Configuración de Políticas del Club (<see cref="PoliticaReserva"/>).
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class CanchasStockController : ControllerBase
{
    private static readonly string[] DefaultDays =
    [
        "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"
    ];

    private const string DefaultCourtPhoto =
        "https://images.unsplash.com/photo-1595435934249-5df7ed86e1c0?auto=format&fit=crop&w=600&q=80";

    private readonly TenisAhoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ClubConfigOptions _clubConfig;

    public CanchasStockController(
        TenisAhoraDbContext dbContext,
        ICurrentUserService currentUser,
        IOptions<ClubConfigOptions> clubConfigOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clubConfig = clubConfigOptions.Value;
    }

    // =========================================================================
    // 1. CANCHAS (/api/canchas)
    // =========================================================================

    [HttpGet("canchas")]
    public async Task<IActionResult> GetCanchas(CancellationToken cancellationToken)
    {
        var canchas = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        return Ok(canchas.Select(MapCanchaDto).ToList());
    }

    [HttpGet("canchas/{id}")]
    public async Task<IActionResult> GetCanchaById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var cancha = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cancha is null)
        {
            throw DomainException.NotFound($"No se encontró en `canchas` el registro con id `{id}`.");
        }

        return Ok(MapCanchaDto(cancha));
    }

    [HttpPost("canchas")]
    public async Task<IActionResult> CreateCancha(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre de la cancha es obligatorio.");
        }

        var surfaceType = GetString(body, "surfaceType", "Ladrillo").Trim();
        var tipoCancha = await ResolveOrCreateTipoCanchaAsync(surfaceType, cancellationToken);

        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"cancha-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var status = GetString(body, "status", "disponible");
        var cancha = new Cancha
        {
            Id = id,
            TipoCanchaId = tipoCancha.Id,
            Nombre = name,
            Superficie = tipoCancha.Nombre,
            CapacidadJugadores = GetInt(body, "capacity", 4),
            PrecioPorHora = GetDecimal(body, "pricePerHour", tipoCancha.PrecioBaseHora),
            Estado = status,
            Descripcion = GetString(body, "description"),
            NotasMantenimiento = GetString(body, "maintenanceNotes"),
            FotoUrl = GetString(body, "photo", DefaultCourtPhoto),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Canchas.Add(cancha);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (body.TryGetProperty("availability", out var avElement) && avElement.ValueKind == JsonValueKind.Object)
        {
            await ApplyCourtAvailabilityInternalAsync(cancha, avElement, cancellationToken);
        }
        else
        {
            await SeedDefaultCourtScheduleAsync(cancha.Id, cancellationToken);
        }

        var created = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstAsync(c => c.Id == cancha.Id, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapCanchaDto(created));
    }

    [HttpPut("canchas/{id}")]
    public async Task<IActionResult> UpdateCancha(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var cancha = await _dbContext.Canchas
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cancha is null)
        {
            throw DomainException.NotFound($"No se encontró en `canchas` el registro con id `{id}`.");
        }

        if (body.TryGetProperty("name", out _))
        {
            var nextName = GetString(body, "name", cancha.Nombre).Trim();
            if (!string.IsNullOrWhiteSpace(nextName))
            {
                cancha.Nombre = nextName;
            }
        }

        if (body.TryGetProperty("surfaceType", out _))
        {
            var nextSurface = GetString(body, "surfaceType", cancha.Superficie).Trim();
            var tipoCancha = await ResolveOrCreateTipoCanchaAsync(nextSurface, cancellationToken);
            cancha.TipoCanchaId = tipoCancha.Id;
            cancha.Superficie = tipoCancha.Nombre;
        }

        if (body.TryGetProperty("capacity", out _))
        {
            cancha.CapacidadJugadores = Math.Max(GetInt(body, "capacity", cancha.CapacidadJugadores), 2);
        }

        if (body.TryGetProperty("pricePerHour", out _))
        {
            cancha.PrecioPorHora = Math.Max(GetDecimal(body, "pricePerHour", cancha.PrecioPorHora), 0m);
        }

        if (body.TryGetProperty("status", out _))
        {
            cancha.Estado = GetString(body, "status", cancha.Estado);
        }

        if (body.TryGetProperty("description", out _))
        {
            cancha.Descripcion = GetString(body, "description");
        }

        if (body.TryGetProperty("maintenanceNotes", out _))
        {
            cancha.NotasMantenimiento = GetString(body, "maintenanceNotes");
        }

        if (body.TryGetProperty("photo", out _))
        {
            var photo = GetString(body, "photo");
            if (!string.IsNullOrWhiteSpace(photo))
            {
                cancha.FotoUrl = photo;
            }
        }

        cancha.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (body.TryGetProperty("availability", out var avElement) && avElement.ValueKind == JsonValueKind.Object)
        {
            await ApplyCourtAvailabilityInternalAsync(cancha, avElement, cancellationToken);
        }

        var updated = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstAsync(c => c.Id == id, cancellationToken);

        return Ok(MapCanchaDto(updated));
    }

    [HttpDelete("canchas/{id}")]
    public async Task<IActionResult> DeleteCancha(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var cancha = await _dbContext.Canchas
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cancha is null)
        {
            throw DomainException.NotFound($"No se encontró en `canchas` el registro con id `{id}`.");
        }

        var hasActiveReservations = await _dbContext.Reservas
            .AnyAsync(r => r.CanchaId == id && r.Estado != "cancelada", cancellationToken);
        if (hasActiveReservations)
        {
            throw DomainException.Conflict("No se puede eliminar la cancha porque posee reservas registradas.");
        }

        _dbContext.Canchas.Remove(cancha);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// PATCH /api/canchas/{id}/estado — Actualiza el estado operativo de la cancha (<c>disponible</c>, <c>ocupada</c>, <c>mantenimiento</c>)
    /// y sincroniza los registros de <see cref="BloqueoMantenimiento"/> (RD06).
    /// </summary>
    [HttpPatch("canchas/{id}/estado")]
    public async Task<IActionResult> UpdateCanchaEstado(
        [FromRoute] string id,
        [FromBody] UpdateCourtStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var status = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is not ("disponible" or "ocupada" or "mantenimiento"))
        {
            throw DomainException.BadRequest("status debe ser disponible, ocupada o mantenimiento.");
        }

        var cancha = await _dbContext.Canchas
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cancha is null)
        {
            throw DomainException.NotFound($"No se encontró en `canchas` el registro con id `{id}`.");
        }

        cancha.Estado = status;
        cancha.NotasMantenimiento = request.MaintenanceNotes ?? string.Empty;
        cancha.UpdatedAt = DateTimeOffset.UtcNow;

        if (status == "mantenimiento")
        {
            var activeBlock = cancha.BloqueosMantenimiento
                .FirstOrDefault(b => b.Activo && string.IsNullOrEmpty(b.DiaSemana));

            if (activeBlock is null)
            {
                _dbContext.BloqueosMantenimiento.Add(new BloqueoMantenimiento
                {
                    Id = $"blq-{id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    CanchaId = id,
                    DiaSemana = null,
                    FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
                    FechaFin = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    HoraInicio = new TimeOnly(8, 0),
                    HoraFin = new TimeOnly(22, 0),
                    Motivo = !string.IsNullOrWhiteSpace(cancha.NotasMantenimiento)
                        ? cancha.NotasMantenimiento
                        : "Mantenimiento general de superficie",
                    Activo = true
                });
            }
            else if (!string.IsNullOrWhiteSpace(cancha.NotasMantenimiento))
            {
                activeBlock.Motivo = cancha.NotasMantenimiento;
                activeBlock.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else if (status == "disponible")
        {
            foreach (var b in cancha.BloqueosMantenimiento.Where(b => b.Activo && string.IsNullOrEmpty(b.DiaSemana)))
            {
                b.Activo = false;
                b.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstAsync(c => c.Id == id, cancellationToken);

        return Ok(MapCanchaDto(updated));
    }

    /// <summary>
    /// PUT /api/canchas/{id}/disponibilidad — Actualiza los horarios de apertura/cierre por día (<see cref="HorarioCancha"/>)
    /// y las franjas bloqueadas (<see cref="BloqueoMantenimiento"/>) de la cancha.
    /// </summary>
    [HttpPut("canchas/{id}/disponibilidad")]
    public async Task<IActionResult> UpdateCanchaDisponibilidad(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var cancha = await _dbContext.Canchas
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cancha is null)
        {
            throw DomainException.NotFound($"No se encontró en `canchas` el registro con id `{id}`.");
        }

        await ApplyCourtAvailabilityInternalAsync(cancha, body, cancellationToken);

        var updated = await _dbContext.Canchas
            .AsNoTracking()
            .Include(c => c.TipoCancha)
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstAsync(c => c.Id == id, cancellationToken);

        return Ok(MapCanchaDto(updated));
    }

    // =========================================================================
    // 2. TIPOS DE CANCHA (/api/tipos-cancha)
    // =========================================================================

    [HttpGet("tipos-cancha")]
    public async Task<IActionResult> GetTiposCancha(CancellationToken cancellationToken)
    {
        var tipos = await _dbContext.TiposCancha
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return Ok(tipos.Select(MapTipoCanchaDto).ToList());
    }

    [HttpGet("tipos-cancha/{id}")]
    public async Task<IActionResult> GetTipoCanchaById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var tipo = await _dbContext.TiposCancha
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-cancha` el registro con id `{id}`.");
        }

        return Ok(MapTipoCanchaDto(tipo));
    }

    [HttpPost("tipos-cancha")]
    public async Task<IActionResult> CreateTipoCancha(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre del tipo de cancha es obligatorio.");
        }

        var exists = await _dbContext.TiposCancha
            .AnyAsync(t => t.Nombre.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
        {
            throw DomainException.Conflict("Ya existe un tipo de cancha con ese nombre.");
        }

        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"ct-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var rawMaxStudents = GetInt(body, "maxStudentsPerClass", 30);
        var maxStudents = rawMaxStudents <= 0 ? 30 : Math.Min(rawMaxStudents, 30);

        var tipo = new TipoCancha
        {
            Id = id,
            Nombre = name,
            Etiqueta = GetString(body, "label", name),
            Descripcion = GetString(body, "description"),
            VelocidadSuperficie = GetString(body, "speed", "Media"),
            PrecioBaseHora = Math.Max(GetDecimal(body, "basePricePerHour", 0m), 0m),
            FrecuenciaMantenimiento = GetString(body, "maintenanceFrequency"),
            MaxAlumnosPorClase = maxStudents,
            Activo = GetBool(body, "active", true),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.TiposCancha.Add(tipo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapTipoCanchaDto(tipo));
    }

    [HttpPut("tipos-cancha/{id}")]
    public async Task<IActionResult> UpdateTipoCancha(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var tipo = await _dbContext.TiposCancha
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-cancha` el registro con id `{id}`.");
        }

        var nextName = GetString(body, "name", tipo.Nombre).Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw DomainException.BadRequest("El nombre del tipo de cancha es obligatorio.");
        }

        var existsOther = await _dbContext.TiposCancha
            .AnyAsync(t => t.Id != id && t.Nombre.ToLower() == nextName.ToLower(), cancellationToken);
        if (existsOther)
        {
            throw DomainException.Conflict("Ya existe otro tipo de cancha con ese nombre.");
        }

        tipo.Nombre = nextName;
        if (body.TryGetProperty("label", out _))
        {
            tipo.Etiqueta = GetString(body, "label", nextName);
        }
        if (body.TryGetProperty("description", out _))
        {
            tipo.Descripcion = GetString(body, "description");
        }
        if (body.TryGetProperty("speed", out _))
        {
            tipo.VelocidadSuperficie = GetString(body, "speed", "Media");
        }
        if (body.TryGetProperty("basePricePerHour", out _))
        {
            tipo.PrecioBaseHora = Math.Max(GetDecimal(body, "basePricePerHour", tipo.PrecioBaseHora), 0m);
        }
        if (body.TryGetProperty("maintenanceFrequency", out _))
        {
            tipo.FrecuenciaMantenimiento = GetString(body, "maintenanceFrequency");
        }
        if (body.TryGetProperty("maxStudentsPerClass", out _))
        {
            var rawMax = GetInt(body, "maxStudentsPerClass", tipo.MaxAlumnosPorClase);
            tipo.MaxAlumnosPorClase = rawMax <= 0 ? 30 : Math.Min(rawMax, 30);
        }
        if (body.TryGetProperty("active", out _))
        {
            tipo.Activo = GetBool(body, "active", tipo.Activo);
        }

        tipo.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapTipoCanchaDto(tipo));
    }

    [HttpDelete("tipos-cancha/{id}")]
    public async Task<IActionResult> DeleteTipoCancha(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var tipo = await _dbContext.TiposCancha
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-cancha` el registro con id `{id}`.");
        }

        var usedByCourts = await _dbContext.Canchas
            .AnyAsync(c => c.TipoCanchaId == id || c.Superficie.ToLower() == tipo.Nombre.ToLower(), cancellationToken);
        if (usedByCourts)
        {
            throw DomainException.Conflict($"No se puede eliminar: existen canchas registradas con superficie \"{tipo.Nombre}\".");
        }

        _dbContext.TiposCancha.Remove(tipo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    // =========================================================================
    // 3. STOCK E INVENTARIO (/api/stock)
    // =========================================================================

    [HttpGet("stock")]
    public async Task<IActionResult> GetStock(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Stocks
            .AsNoTracking()
            .Include(s => s.Producto)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapStockDto).ToList());
    }

    [HttpGet("stock/{id}")]
    public async Task<IActionResult> GetStockById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var stock = await _dbContext.Stocks
            .AsNoTracking()
            .Include(s => s.Producto)
            .FirstOrDefaultAsync(s => s.Id == id || s.ProductoId == id, cancellationToken);

        if (stock is null)
        {
            throw DomainException.NotFound($"No se encontró en `stock` el registro con id `{id}`.");
        }

        return Ok(MapStockDto(stock));
    }

    [HttpPost("stock")]
    public async Task<IActionResult> CreateStock(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre del ítem de stock es obligatorio.");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var stockId = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(stockId))
        {
            stockId = $"stock-{nowMs}";
        }

        var prodId = $"prod-{nowMs}";
        var sku = $"SKU-{nowMs % 100000:00000}";
        var category = GetString(body, "category", "Equipamiento");
        var unit = GetString(body, "unit", "unidades");
        var description = GetString(body, "description");
        var itemPrice = Math.Max(GetDecimal(body, "itemPrice", 0m), 0m);

        var totalStock = Math.Max(GetInt(body, "totalStock", 0), 0);
        var inUseStock = Math.Max(GetInt(body, "inUseStock", 0), 0);
        if (totalStock < inUseStock)
        {
            totalStock = inUseStock;
        }
        var availableStock = Math.Max(totalStock - inUseStock, 0);
        var status = availableStock <= 0 ? "agotado" : "disponible";

        var producto = new Producto
        {
            Id = prodId,
            CodigoSku = sku,
            Nombre = name,
            Categoria = category,
            Descripcion = description,
            UnidadMedida = unit,
            PrecioUnitarioAlquilerOVenta = itemPrice,
            EsObligatorioEnReserva = category is "Redes" or "Pelotas" or "Raquetas",
            Activo = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var stock = new Stock
        {
            Id = stockId,
            ProductoId = prodId,
            StockTotal = totalStock,
            StockEnUso = inUseStock,
            StockDisponible = availableStock,
            UmbralMinimoAlerta = 2,
            Estado = status,
            UltimaActualizacion = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Producto = producto
        };

        _dbContext.Productos.Add(producto);
        _dbContext.Stocks.Add(stock);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapStockDto(stock));
    }

    [HttpPut("stock/{id}")]
    public async Task<IActionResult> UpdateStock(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var stock = await _dbContext.Stocks
            .Include(s => s.Producto)
            .FirstOrDefaultAsync(s => s.Id == id || s.ProductoId == id, cancellationToken);

        if (stock is null)
        {
            throw DomainException.NotFound($"No se encontró en `stock` el registro con id `{id}`.");
        }

        if (stock.Producto is not null)
        {
            if (body.TryGetProperty("name", out _))
            {
                var nextName = GetString(body, "name", stock.Producto.Nombre).Trim();
                if (!string.IsNullOrWhiteSpace(nextName))
                {
                    stock.Producto.Nombre = nextName;
                }
            }
            if (body.TryGetProperty("category", out _))
            {
                stock.Producto.Categoria = GetString(body, "category", stock.Producto.Categoria);
            }
            if (body.TryGetProperty("description", out _))
            {
                stock.Producto.Descripcion = GetString(body, "description");
            }
            if (body.TryGetProperty("unit", out _))
            {
                stock.Producto.UnidadMedida = GetString(body, "unit", stock.Producto.UnidadMedida);
            }
            if (body.TryGetProperty("itemPrice", out _))
            {
                stock.Producto.PrecioUnitarioAlquilerOVenta = Math.Max(GetDecimal(body, "itemPrice", stock.Producto.PrecioUnitarioAlquilerOVenta), 0m);
            }
            stock.Producto.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var totalStock = body.TryGetProperty("totalStock", out _)
            ? Math.Max(GetInt(body, "totalStock", stock.StockTotal), 0)
            : stock.StockTotal;

        var inUseStock = body.TryGetProperty("inUseStock", out _)
            ? Math.Max(GetInt(body, "inUseStock", stock.StockEnUso), 0)
            : stock.StockEnUso;

        if (totalStock < inUseStock)
        {
            throw DomainException.BadRequest(
                $"El stock total ({totalStock}) no puede ser menor al stock actualmente en uso ({inUseStock}).");
        }

        stock.StockTotal = totalStock;
        stock.StockEnUso = inUseStock;
        stock.StockDisponible = Math.Max(totalStock - inUseStock, 0);
        stock.Estado = stock.StockDisponible <= 0 ? "agotado" : "disponible";
        stock.UltimaActualizacion = DateTimeOffset.UtcNow;
        stock.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapStockDto(stock));
    }

    [HttpDelete("stock/{id}")]
    public async Task<IActionResult> DeleteStock(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var stock = await _dbContext.Stocks
            .Include(s => s.Producto)
            .FirstOrDefaultAsync(s => s.Id == id || s.ProductoId == id, cancellationToken);

        if (stock is null)
        {
            throw DomainException.NotFound($"No se encontró en `stock` el registro con id `{id}`.");
        }

        if (stock.Producto is not null)
        {
            _dbContext.Productos.Remove(stock.Producto);
        }
        else
        {
            _dbContext.Stocks.Remove(stock);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// PATCH /api/stock/{id}/ajuste — Ajusta <c>StockTotal</c> validando que <c>totalStock &gt;= StockEnUso</c>
    /// y recalcula <c>StockDisponible = totalStock - StockEnUso</c>.
    /// </summary>
    [HttpPatch("stock/{id}/ajuste")]
    public async Task<IActionResult> AdjustStock(
        [FromRoute] string id,
        [FromBody] AdjustStockRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var stock = await _dbContext.Stocks
            .Include(s => s.Producto)
            .FirstOrDefaultAsync(s => s.Id == id || s.ProductoId == id, cancellationToken);

        if (stock is null)
        {
            throw DomainException.NotFound($"No se encontró en `stock` el registro con id `{id}`.");
        }

        var nextTotal = Math.Max(request.TotalStock, 0);
        if (nextTotal < stock.StockEnUso)
        {
            throw DomainException.BadRequest(
                $"El stock total ({nextTotal}) no puede ser inferior al stock actualmente en uso ({stock.StockEnUso}).");
        }

        stock.StockTotal = nextTotal;
        stock.StockDisponible = Math.Max(nextTotal - stock.StockEnUso, 0);
        stock.Estado = stock.StockDisponible <= 0 ? "agotado" : "disponible";
        stock.UltimaActualizacion = DateTimeOffset.UtcNow;
        stock.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapStockDto(stock));
    }

    // =========================================================================
    // 4. CONFIGURACIÓN DEL CLUB (/api/configuracion)
    // =========================================================================

    /// <summary>
    /// GET /api/configuracion — Devuelve la <see cref="PoliticaReserva"/> activa del club (RD01 a RD05).
    /// </summary>
    [HttpGet("configuracion")]
    public async Task<IActionResult> GetClubConfig(CancellationToken cancellationToken)
    {
        var politica = await _dbContext.PoliticasReserva
            .AsNoTracking()
            .Where(p => p.Activa)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var freeCancelHours = politica is not null
            ? (double)politica.HorasCancelacionGratuita
            : _clubConfig.FreeCancellationHours;
        var retentionPct = politica?.PorcentajeRetencionCancelacionTardia ?? _clubConfig.LateCancellationRetentionPercent;
        var maxAdvanceDays = politica?.DiasMaxAntelacion ?? _clubConfig.MaxAdvanceBookingDays;
        var maxDurationHours = politica?.DuracionMaximaHoras ?? 2.0m;
        var depositPercent = politica?.PorcentajeSenaObligatoria ?? 50.0m;

        return Ok(new
        {
            id = "club",
            policyId = politica?.Id ?? "pol-estandar",
            policyName = politica?.NombrePolitica ?? "Política Oficial Club Tenis Ahora (IEEE SRS-830)",
            freeCancellationHours = freeCancelHours,
            lateCancellationRetentionPercent = retentionPct,
            maxAdvanceBookingDays = maxAdvanceDays,
            privateLessonHourlyRate = _clubConfig.PrivateLessonHourlyRate,
            maxAdvanceDays,
            maxDurationHours,
            freeCancelHours,
            depositPercent
        });
    }

    // =========================================================================
    // MÉTODOS AUXILIARES PRIVADOS
    // =========================================================================

    private void EnsureStaff()
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }
    }

    private async Task<TipoCancha> ResolveOrCreateTipoCanchaAsync(
        string surfaceName,
        CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(surfaceName) ? "Ladrillo" : surfaceName.Trim();
        var existing = await _dbContext.TiposCancha
            .FirstOrDefaultAsync(t => t.Nombre.ToLower() == normalized.ToLower(), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var newTipo = new TipoCancha
        {
            Id = $"ct-{normalized.ToLowerInvariant().Replace(' ', '-')}",
            Nombre = normalized,
            Etiqueta = normalized,
            Descripcion = $"Superficie {normalized}",
            VelocidadSuperficie = "Media",
            PrecioBaseHora = 4800m,
            MaxAlumnosPorClase = 30,
            Activo = true
        };

        _dbContext.TiposCancha.Add(newTipo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return newTipo;
    }

    private async Task SeedDefaultCourtScheduleAsync(string canchaId, CancellationToken cancellationToken)
    {
        foreach (var day in DefaultDays)
        {
            _dbContext.HorariosCancha.Add(new HorarioCancha
            {
                Id = $"hc-{canchaId}-{day[..3].ToLowerInvariant()}",
                CanchaId = canchaId,
                DiaSemana = day,
                HoraApertura = new TimeOnly(8, 0),
                HoraCierre = new TimeOnly(22, 0),
                DuracionTurnoMinutos = 60,
                Habilitado = true,
                Notas = "Horario corrido"
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyCourtAvailabilityInternalAsync(
        Cancha cancha,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var currentAv = BuildAvailabilityObject(cancha);

        var days = body.TryGetProperty("days", out var daysProp) && daysProp.ValueKind == JsonValueKind.Array
            ? daysProp.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
            : currentAv.Days;

        if (days.Count == 0)
        {
            throw DomainException.BadRequest("Debe indicar al menos un día de atención.");
        }

        var openTimeStr = GetString(body, "openTime", currentAv.OpenTime);
        var closeTimeStr = GetString(body, "closeTime", currentAv.CloseTime);

        if (string.Compare(closeTimeStr, openTimeStr, StringComparison.Ordinal) <= 0)
        {
            throw DomainException.BadRequest("El horario de cierre debe ser posterior al de apertura.");
        }

        var openTime = TimeOnly.TryParseExact(openTimeStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOpen)
            ? parsedOpen
            : new TimeOnly(8, 0);

        var closeTime = TimeOnly.TryParseExact(closeTimeStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedClose)
            ? parsedClose
            : new TimeOnly(22, 0);

        var slotMinutes = GetInt(body, "slotMinutes", currentAv.SlotMinutes);
        if (slotMinutes <= 0)
        {
            slotMinutes = 60;
        }

        var notes = GetString(body, "notes", currentAv.Notes);

        var existingHorarios = await _dbContext.HorariosCancha
            .Where(h => h.CanchaId == cancha.Id)
            .ToListAsync(cancellationToken);
        _dbContext.HorariosCancha.RemoveRange(existingHorarios);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var idx = 0;
        foreach (var day in days)
        {
            idx++;
            _dbContext.HorariosCancha.Add(new HorarioCancha
            {
                Id = $"hc-{cancha.Id}-{nowMs}-{idx}",
                CanchaId = cancha.Id,
                DiaSemana = day,
                HoraApertura = openTime,
                HoraCierre = closeTime,
                DuracionTurnoMinutos = slotMinutes,
                Habilitado = true,
                Notas = notes
            });
        }

        if (body.TryGetProperty("blockedRanges", out var blProp) && blProp.ValueKind == JsonValueKind.Array)
        {
            var existingDayBlocks = await _dbContext.BloqueosMantenimiento
                .Where(b => b.CanchaId == cancha.Id && b.DiaSemana != null)
                .ToListAsync(cancellationToken);
            _dbContext.BloqueosMantenimiento.RemoveRange(existingDayBlocks);

            var bIdx = 0;
            foreach (var item in blProp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                bIdx++;
                var bDay = GetString(item, "day", "Lunes");
                var bFromStr = GetString(item, "from", "12:00");
                var bToStr = GetString(item, "to", "13:00");
                var bReason = GetString(item, "reason", "Bloqueo horario");

                var bFrom = TimeOnly.TryParseExact(bFromStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var pf)
                    ? pf
                    : new TimeOnly(12, 0);
                var bTo = TimeOnly.TryParseExact(bToStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var pt)
                    ? pt
                    : new TimeOnly(13, 0);

                if (bTo <= bFrom)
                {
                    bTo = bFrom.AddHours(1);
                }

                _dbContext.BloqueosMantenimiento.Add(new BloqueoMantenimiento
                {
                    Id = $"blq-{cancha.Id}-{nowMs}-{bIdx}",
                    CanchaId = cancha.Id,
                    DiaSemana = bDay,
                    FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
                    FechaFin = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                    HoraInicio = bFrom,
                    HoraFin = bTo,
                    Motivo = string.IsNullOrWhiteSpace(bReason) ? "Bloqueo programado" : bReason,
                    Activo = true
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static object MapCanchaDto(Cancha c)
    {
        var av = BuildAvailabilityObject(c);
        return new
        {
            id = c.Id,
            name = c.Nombre,
            surfaceType = c.Superficie,
            capacity = c.CapacidadJugadores,
            pricePerHour = c.PrecioPorHora,
            status = c.Estado,
            description = c.Descripcion ?? string.Empty,
            maintenanceNotes = c.NotasMantenimiento ?? string.Empty,
            availability = new
            {
                days = av.Days,
                openTime = av.OpenTime,
                closeTime = av.CloseTime,
                slotMinutes = av.SlotMinutes,
                blockedRanges = av.BlockedRanges,
                notes = av.Notes
            },
            schedule = Array.Empty<object>(),
            photo = !string.IsNullOrWhiteSpace(c.FotoUrl) ? c.FotoUrl : DefaultCourtPhoto
        };
    }

    private static (List<string> Days, string OpenTime, string CloseTime, int SlotMinutes, List<object> BlockedRanges, string Notes)
        BuildAvailabilityObject(Cancha c)
    {
        var enabledSchedules = c.HorariosCancha.Where(h => h.Habilitado).ToList();
        var days = enabledSchedules.Count > 0
            ? enabledSchedules.Select(h => h.DiaSemana).Distinct().ToList()
            : DefaultDays.ToList();

        // Si en el seed SQL solo se cargó "Lunes" como muestra representativa, exponer toda la semana salvo que se haya personalizado
        if (days.Count == 1 && days[0] == "Lunes" && enabledSchedules.All(h => h.Id.EndsWith("-lun", StringComparison.OrdinalIgnoreCase)))
        {
            days = DefaultDays.ToList();
        }

        var firstSchedule = enabledSchedules.FirstOrDefault();
        var openTime = firstSchedule?.HoraApertura.ToString("HH:mm") ?? "08:00";
        var closeTime = firstSchedule?.HoraCierre.ToString("HH:mm") ?? "22:00";
        var slotMinutes = firstSchedule?.DuracionTurnoMinutos ?? 60;
        var notes = firstSchedule?.Notas ?? string.Empty;

        var blockedRanges = c.BloqueosMantenimiento
            .Where(b => b.Activo && !string.IsNullOrWhiteSpace(b.DiaSemana))
            .Select(b => (object)new
            {
                day = b.DiaSemana!,
                from = b.HoraInicio.ToString("HH:mm"),
                to = b.HoraFin.ToString("HH:mm"),
                reason = b.Motivo
            })
            .ToList();

        return (days, openTime, closeTime, slotMinutes, blockedRanges, notes);
    }

    private static object MapTipoCanchaDto(TipoCancha t) => new
    {
        id = t.Id,
        name = t.Nombre,
        label = t.Etiqueta,
        description = t.Descripcion ?? string.Empty,
        speed = t.VelocidadSuperficie ?? "Media",
        basePricePerHour = t.PrecioBaseHora,
        maintenanceFrequency = t.FrecuenciaMantenimiento ?? string.Empty,
        maxStudentsPerClass = t.MaxAlumnosPorClase,
        active = t.Activo
    };

    private static object MapStockDto(Stock s) => new
    {
        id = s.Id,
        productId = s.ProductoId,
        name = s.Producto?.Nombre ?? s.Id,
        category = s.Producto?.Categoria ?? "Equipamiento",
        totalStock = s.StockTotal,
        inUseStock = s.StockEnUso,
        availableStock = s.StockDisponible,
        unit = s.Producto?.UnidadMedida ?? "unidades",
        itemPrice = s.Producto?.PrecioUnitarioAlquilerOVenta ?? 0m,
        status = s.StockDisponible <= 0 ? "agotado" : s.Estado,
        description = s.Producto?.Descripcion ?? string.Empty
    };

    private static string GetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? defaultValue,
                JsonValueKind.Number => prop.GetRawText(),
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private static int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
            {
                return val;
            }
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var decVal))
            {
                return (int)Math.Round(decVal);
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        return defaultValue;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName, decimal defaultValue = 0m)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val))
            {
                return val;
            }
            if (prop.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return defaultValue;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }
}

public sealed class UpdateCourtStatusRequestDto
{
    public string? Status { get; set; }
    public string? MaintenanceNotes { get; set; }
}

public sealed class AdjustStockRequestDto
{
    public int TotalStock { get; set; }
}

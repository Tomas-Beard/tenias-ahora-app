using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenisAhora.Application.Interfaces;
using TenisAhora.Domain.Entities;
using TenisAhora.Domain.Exceptions;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Controlador REST para el Módulo 6 (IEEE SRS-830): Caja, Recibos (<see cref="Recibo"/>),
/// Descuentos (<see cref="Descuento"/>), Facturas Fiscales (<see cref="Factura"/>),
/// Reembolsos (<see cref="Reembolso"/>), Pagos (<see cref="Pago"/>) y Receptor de Webhook/IPN de Mercado Pago.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class CajaPagosWebhookController : ControllerBase
{
    private static readonly string[] AllowedPaymentMethods =
    [
        "Mercado Pago",
        "Mercado Pago (QR)",
        "Tarjeta de Débito",
        "Tarjeta de Crédito",
        "Billetera Virtual",
        "Efectivo",
        "Retención sobre seña",
        "Crédito en cuenta de socio"
    ];

    private readonly TenisAhoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CajaPagosWebhookController> _logger;

    public CajaPagosWebhookController(
        TenisAhoraDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<CajaPagosWebhookController> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    // =========================================================================
    // 1. RECIBOS DE PAGO (/api/recibos)
    // =========================================================================

    [HttpGet("recibos")]
    public async Task<IActionResult> GetRecibos(CancellationToken cancellationToken)
    {
        var query = _dbContext.Recibos.AsNoTracking();

        if (!_currentUser.IsStaff)
        {
            if (_currentUser.IsMember)
            {
                var dni = _currentUser.Dni;
                query = query.Where(r => r.ClienteDni == dni);
            }
            else
            {
                return Ok(Array.Empty<object>());
            }
        }

        var list = await query
            .OrderByDescending(r => r.FechaEmision)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapReciboDto).ToList());
    }

    [HttpGet("recibos/{id}")]
    public async Task<IActionResult> GetReciboById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var recibo = await _dbContext.Recibos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (recibo is null)
        {
            throw DomainException.NotFound($"No se encontró en `recibos` el registro con id `{id}`.");
        }

        if (!_currentUser.IsStaff && (!_currentUser.IsMember || !_currentUser.IsMeByDni(recibo.ClienteDni)))
        {
            throw DomainException.Forbidden("No tenés acceso a este registro.");
        }

        return Ok(MapReciboDto(recibo));
    }

    [HttpPost("recibos")]
    public async Task<IActionResult> CreateRecibo(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var clientDni = GetString(body, "clientDni", _currentUser.Dni).Trim();
        if (string.IsNullOrWhiteSpace(clientDni))
        {
            clientDni = "00.000.000";
        }

        if (!_currentUser.IsStaff)
        {
            if ((_currentUser.IsMember || _currentUser.IsProfessional) && _currentUser.IsMeByDni(clientDni))
            {
                // Permitido: el socio registra sus propios pagos desde el portal
            }
            else
            {
                throw DomainException.Forbidden("Solo podés registrar pagos a tu nombre.");
            }
        }

        var count = await _dbContext.Recibos.CountAsync(cancellationToken);
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"REC-{DateTime.UtcNow.Year}-{1050 + count}";
        }

        var clientName = GetString(body, "clientName", "Socio Tenis Ahora").Trim();
        var concept = GetString(body, "concept", "Cobro en Caja Club Tenis Ahora").Trim();
        var totalPaid = Math.Max(GetDecimal(body, "totalPaid", 1000m), 1m);
        var paymentMethod = NormalizePaymentMethod(GetString(body, "paymentMethod", "Mercado Pago (QR)"));
        var cashierName = GetString(body, "cashierName", "Administración General");
        var reservationId = GetString(body, "reservationId");
        if (string.IsNullOrWhiteSpace(reservationId))
        {
            reservationId = null;
        }

        var itemsJson = body.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array
            ? itemsProp.GetRawText()
            : JsonSerializer.Serialize(new[] { new { description = concept, amount = totalPaid } });

        var cliente = await ResolveOrCreateClienteAsync(clientDni, clientName, cancellationToken);

        var pago = new Pago
        {
            Id = $"PAG-{id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            ClienteId = cliente.Id,
            ReservaId = reservationId,
            TipoPago = DeterminePaymentType(concept),
            MedioPago = paymentMethod,
            Monto = totalPaid,
            Estado = "completado",
            QrData = $"https://mpago.la/pos/tenisahora/{id.ToLowerInvariant()}",
            FechaPago = DateTimeOffset.UtcNow
        };

        var recibo = new Recibo
        {
            Id = id,
            NumeroRecibo = id,
            PagoId = pago.Id,
            ClienteId = cliente.Id,
            ReservaId = reservationId,
            ClienteNombre = clientName,
            ClienteDni = clientDni,
            Concepto = concept,
            ItemsJson = itemsJson,
            TotalAbonado = totalPaid,
            MedioPago = paymentMethod,
            Estado = GetString(body, "status", "completado"),
            QrData = GetString(body, "qrData", pago.QrData ?? string.Empty),
            CajeroNombre = cashierName,
            FechaEmision = DateTimeOffset.UtcNow
        };

        _dbContext.Pagos.Add(pago);
        _dbContext.Recibos.Add(recibo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapReciboDto(recibo));
    }

    // =========================================================================
    // 2. DESCUENTOS Y BENEFICIOS (/api/descuentos)
    // =========================================================================

    [HttpGet("descuentos")]
    public async Task<IActionResult> GetDescuentos(CancellationToken cancellationToken)
    {
        var list = await _dbContext.Descuentos
            .AsNoTracking()
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapDescuentoDto).ToList());
    }

    [HttpGet("descuentos/{id}")]
    public async Task<IActionResult> GetDescuentoById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var d = await _dbContext.Descuentos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (d is null)
        {
            throw DomainException.NotFound($"No se encontró en `descuentos` el registro con id `{id}`.");
        }

        return Ok(MapDescuentoDto(d));
    }

    [HttpPost("descuentos")]
    public async Task<IActionResult> CreateDescuento(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var code = GetString(body, "code").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DomainException.BadRequest("El código del descuento es obligatorio.");
        }

        var exists = await _dbContext.Descuentos
            .AnyAsync(d => d.Codigo.ToUpper() == code, cancellationToken);
        if (exists)
        {
            throw DomainException.Conflict("Ya existe un descuento con ese código.");
        }

        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"disc-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var pct = Math.Clamp(GetDecimal(body, "discountPercent", 10m), 0m, 100m);

        var descuento = new Descuento
        {
            Id = id,
            Codigo = code,
            Nombre = GetString(body, "name", code).Trim(),
            PorcentajeDescuento = pct,
            RubroAplicable = GetString(body, "applicableTo", "Alquiler de Canchas"),
            Activo = GetBool(body, "active", true),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Descuentos.Add(descuento);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapDescuentoDto(descuento));
    }

    [HttpPut("descuentos/{id}")]
    public async Task<IActionResult> UpdateDescuento(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var descuento = await _dbContext.Descuentos
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (descuento is null)
        {
            throw DomainException.NotFound($"No se encontró en `descuentos` el registro con id `{id}`.");
        }

        var nextCode = GetString(body, "code", descuento.Codigo).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(nextCode))
        {
            throw DomainException.BadRequest("El código del descuento es obligatorio.");
        }

        var codeTaken = await _dbContext.Descuentos
            .AnyAsync(d => d.Id != id && d.Codigo.ToUpper() == nextCode, cancellationToken);
        if (codeTaken)
        {
            throw DomainException.Conflict("Ya existe otro descuento con ese código.");
        }

        descuento.Codigo = nextCode;
        if (body.TryGetProperty("name", out _))
        {
            descuento.Nombre = GetString(body, "name", descuento.Nombre).Trim();
        }
        if (body.TryGetProperty("discountPercent", out _))
        {
            descuento.PorcentajeDescuento = Math.Clamp(GetDecimal(body, "discountPercent", descuento.PorcentajeDescuento), 0m, 100m);
        }
        if (body.TryGetProperty("applicableTo", out _))
        {
            descuento.RubroAplicable = GetString(body, "applicableTo", descuento.RubroAplicable);
        }
        if (body.TryGetProperty("active", out _))
        {
            descuento.Activo = GetBool(body, "active", descuento.Activo);
        }

        descuento.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapDescuentoDto(descuento));
    }

    [HttpDelete("descuentos/{id}")]
    public async Task<IActionResult> DeleteDescuento(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var descuento = await _dbContext.Descuentos
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (descuento is null)
        {
            throw DomainException.NotFound($"No se encontró en `descuentos` el registro con id `{id}`.");
        }

        // Cascada: quitar las asignaciones N:M de este descuento en cliente_descuentos
        var assignments = await _dbContext.ClienteDescuentos
            .Where(cd => cd.DescuentoId == id)
            .ToListAsync(cancellationToken);
        _dbContext.ClienteDescuentos.RemoveRange(assignments);

        _dbContext.Descuentos.Remove(descuento);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    // =========================================================================
    // 3. FACTURAS FISCALES (/api/facturas)
    // =========================================================================

    [HttpGet("facturas")]
    public async Task<IActionResult> GetFacturas(CancellationToken cancellationToken)
    {
        EnsureStaff();

        var list = await _dbContext.Facturas
            .AsNoTracking()
            .OrderByDescending(f => f.FechaEmision)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapFacturaDto).ToList());
    }

    [HttpGet("facturas/{id}")]
    public async Task<IActionResult> GetFacturaById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var f = await _dbContext.Facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (f is null)
        {
            throw DomainException.NotFound($"No se encontró en `facturas` el registro con id `{id}`.");
        }

        return Ok(MapFacturaDto(f));
    }

    [HttpPost("facturas")]
    public async Task<IActionResult> CreateFactura(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        if (!body.TryGetProperty("items", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
        {
            throw DomainException.BadRequest("La factura debe tener al menos un ítem con descripción.");
        }

        var normalizedItems = new List<object>();
        decimal subtotal = 0m;
        var firstDescription = string.Empty;

        foreach (var item in itemsProp.EnumerateArray())
        {
            var desc = GetString(item, "description").Trim();
            if (string.IsNullOrWhiteSpace(desc))
            {
                throw DomainException.BadRequest("La factura debe tener al menos un ítem con descripción.");
            }
            if (string.IsNullOrWhiteSpace(firstDescription))
            {
                firstDescription = desc;
            }

            var qty = GetDecimal(item, "quantity", 1m);
            if (qty <= 0m)
            {
                qty = 1m;
            }
            var unitPrice = GetDecimal(item, "unitPrice", 0m);

            normalizedItems.Add(new
            {
                description = desc,
                quantity = qty,
                unitPrice
            });

            subtotal += qty * unitPrice;
        }

        if (normalizedItems.Count == 0)
        {
            throw DomainException.BadRequest("La factura debe tener al menos un ítem con descripción.");
        }

        if (subtotal <= 0m)
        {
            throw DomainException.BadRequest("El importe de la factura debe ser mayor a cero.");
        }

        var count = await _dbContext.Facturas.CountAsync(cancellationToken);
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"FC-0001-{412 + count + 1:00000000}";
        }

        var clientName = GetString(body, "clientName", "Socio Tenis Ahora").Trim();
        var clientDni = GetString(body, "clientDni", "00.000.000").Trim();
        var clientAddress = GetString(body, "clientAddress", "Quilmes, Buenos Aires");
        var concept = GetString(body, "concept", firstDescription).Trim();
        if (string.IsNullOrWhiteSpace(concept))
        {
            concept = firstDescription;
        }

        var paymentMethod = GetString(body, "paymentMethod", "Tarjeta de Débito");
        var issuedBy = GetString(body, "issuedBy", "Administración General");
        var relatedReceiptId = GetString(body, "relatedReceiptId");
        if (string.IsNullOrWhiteSpace(relatedReceiptId))
        {
            relatedReceiptId = null;
        }

        var cliente = await ResolveOrCreateClienteAsync(clientDni, clientName, cancellationToken);

        var factura = new Factura
        {
            Id = id,
            NumeroFactura = id,
            ClienteId = cliente.Id,
            ReciboRelacionadoId = relatedReceiptId,
            TipoComprobante = "B",
            ClienteNombre = clientName,
            ClienteDniOCuit = clientDni,
            ClienteDireccion = clientAddress,
            Concepto = concept,
            ItemsJson = JsonSerializer.Serialize(normalizedItems),
            Subtotal = subtotal,
            AlicuotaIva = 21.00m,
            MontoIva = 0m,
            Total = subtotal,
            MedioPago = paymentMethod,
            Estado = "emitida",
            EmitidaPor = issuedBy,
            FechaEmision = DateTimeOffset.UtcNow
        };

        _dbContext.Facturas.Add(factura);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapFacturaDto(factura));
    }

    // =========================================================================
    // 4. REEMBOLSOS (/api/reembolsos)
    // =========================================================================

    [HttpGet("reembolsos")]
    public async Task<IActionResult> GetReembolsos(CancellationToken cancellationToken)
    {
        EnsureStaff();

        var list = await _dbContext.Reembolsos
            .AsNoTracking()
            .OrderByDescending(r => r.FechaReembolso)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapReembolsoDto).ToList());
    }

    [HttpGet("reembolsos/{id}")]
    public async Task<IActionResult> GetReembolsoById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var r = await _dbContext.Reembolsos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (r is null)
        {
            throw DomainException.NotFound($"No se encontró en `reembolsos` el registro con id `{id}`.");
        }

        return Ok(MapReembolsoDto(r));
    }

    [HttpPost("reembolsos")]
    public async Task<IActionResult> CreateReembolso(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var originalAmount = GetDecimal(body, "originalAmount", 0m);
        var requestedRefund = GetDecimal(body, "refundAmount", 0m);
        var refundAmount = originalAmount > 0m
            ? Math.Min(requestedRefund, originalAmount)
            : requestedRefund;

        if (refundAmount <= 0m)
        {
            throw DomainException.BadRequest("El importe a reembolsar debe ser mayor a cero.");
        }

        if (originalAmount < refundAmount)
        {
            originalAmount = refundAmount;
        }

        var refundType = refundAmount >= originalAmount ? "total" : "parcial";
        var count = await _dbContext.Reembolsos.CountAsync(cancellationToken);
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"RMB-{DateTime.UtcNow.Year}-{count + 9:0000}";
        }

        var clientName = GetString(body, "clientName", "Socio Tenis Ahora").Trim();
        var clientDni = GetString(body, "clientDni", "00.000.000").Trim();
        var reason = GetString(body, "reason", "Reembolso administrativo").Trim();
        var refundMethod = GetString(body, "refundMethod", "Crédito en cuenta de socio");
        var processedBy = GetString(body, "processedBy", "Administración General");

        var relatedReservationId = GetString(body, "relatedReservationId");
        if (string.IsNullOrWhiteSpace(relatedReservationId))
        {
            relatedReservationId = null;
        }

        var relatedReceiptId = GetString(body, "relatedReceiptId");
        if (string.IsNullOrWhiteSpace(relatedReceiptId))
        {
            relatedReceiptId = null;
        }

        var cliente = await ResolveOrCreateClienteAsync(clientDni, clientName, cancellationToken);

        var reembolso = new Reembolso
        {
            Id = id,
            NumeroReembolso = id,
            ClienteId = cliente.Id,
            ReservaRelacionadaId = relatedReservationId,
            ReciboRelacionadoId = relatedReceiptId,
            ClienteNombre = clientName,
            ClienteDni = clientDni,
            Motivo = string.IsNullOrWhiteSpace(reason) ? "Reembolso administrativo" : reason,
            TipoReembolso = refundType,
            MontoOriginal = originalAmount,
            MontoReembolsado = refundAmount,
            MedioDevolucion = refundMethod,
            Estado = "procesado",
            ProcesadoPor = processedBy,
            FechaReembolso = DateTimeOffset.UtcNow
        };

        _dbContext.Reembolsos.Add(reembolso);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapReembolsoDto(reembolso));
    }

    // =========================================================================
    // 5. WEBHOOK / IPN DE MERCADO PAGO (/api/pagos/webhook)
    // =========================================================================

    /// <summary>
    /// POST /api/pagos/webhook — Receptor asíncrono de notificaciones Webhook/IPN de Mercado Pago.
    /// Busca el <see cref="Pago"/> por <c>MpPreferenceId</c> o <c>ExternalReference</c>/<c>ReservaId</c>,
    /// acredita el pago (<c>Estado = "completado"</c>, <c>MpStatus = "aprobado"</c>), asigna <c>MpPaymentId</c>
    /// y confirma la <see cref="Reserva"/> asociada.
    /// </summary>
    [HttpPost("pagos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleMercadoPagoWebhook(
        [FromBody] JsonElement? body,
        [FromQuery(Name = "id")] string? queryId,
        [FromQuery(Name = "data.id")] string? queryDataId,
        [FromQuery(Name = "topic")] string? queryTopic,
        [FromQuery(Name = "type")] string? queryType,
        [FromQuery(Name = "preference_id")] string? queryPreferenceId,
        [FromQuery(Name = "external_reference")] string? queryExternalReference,
        CancellationToken cancellationToken)
    {
        var paymentId = queryDataId ?? queryId;
        var preferenceId = queryPreferenceId;
        var externalReference = queryExternalReference;
        var topic = queryTopic ?? queryType ?? "payment";

        if (body.HasValue && body.Value.ValueKind == JsonValueKind.Object)
        {
            var root = body.Value;
            var bodyType = GetString(root, "type", GetString(root, "topic"));
            if (!string.IsNullOrWhiteSpace(bodyType))
            {
                topic = bodyType;
            }

            if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
            {
                var nestedId = GetString(dataProp, "id");
                if (!string.IsNullOrWhiteSpace(nestedId))
                {
                    paymentId = nestedId;
                }
            }

            if (string.IsNullOrWhiteSpace(paymentId))
            {
                paymentId = GetString(root, "mpPaymentId", GetString(root, "paymentId", GetString(root, "id")));
            }

            if (string.IsNullOrWhiteSpace(preferenceId))
            {
                preferenceId = GetString(root, "preferenceId", GetString(root, "mpPreferenceId", GetString(root, "preference_id")));
            }

            if (string.IsNullOrWhiteSpace(externalReference))
            {
                externalReference = GetString(root, "externalReference", GetString(root, "reservaId", GetString(root, "reservationId", GetString(root, "external_reference"))));
            }
        }

        _logger.LogInformation(
            "Webhook Mercado Pago recibido: topic={Topic}, paymentId={PaymentId}, preferenceId={PreferenceId}, externalRef={ExternalRef}",
            topic, paymentId, preferenceId, externalReference);

        Pago? pago = null;

        if (!string.IsNullOrWhiteSpace(preferenceId))
        {
            pago = await _dbContext.Pagos
                .Include(p => p.Reserva)
                .Include(p => p.Recibo)
                .FirstOrDefaultAsync(p => p.MpPreferenceId == preferenceId, cancellationToken);
        }

        if (pago is null && !string.IsNullOrWhiteSpace(externalReference))
        {
            pago = await _dbContext.Pagos
                .Include(p => p.Reserva)
                .Include(p => p.Recibo)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(p => p.ReservaId == externalReference || p.Id == externalReference, cancellationToken);
        }

        if (pago is null && !string.IsNullOrWhiteSpace(paymentId))
        {
            pago = await _dbContext.Pagos
                .Include(p => p.Reserva)
                .Include(p => p.Recibo)
                .FirstOrDefaultAsync(p =>
                    p.MpPaymentId == paymentId ||
                    p.MpPreferenceId == paymentId ||
                    p.ReservaId == paymentId ||
                    p.Id == paymentId,
                    cancellationToken);
        }

        if (pago is null)
        {
            return Ok(new
            {
                received = true,
                matched = false,
                status = "ignored",
                message = "Notificación recibida correctamente; no se encontró un pago pendiente coincidente."
            });
        }

        var resolvedMpPaymentId = !string.IsNullOrWhiteSpace(paymentId)
            ? paymentId
            : pago.MpPaymentId ?? $"MP-PAY-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        pago.MpPaymentId = resolvedMpPaymentId;
        pago.MpStatus = "aprobado";
        pago.Estado = "completado";
        pago.FechaPago = DateTimeOffset.UtcNow;
        pago.UpdatedAt = DateTimeOffset.UtcNow;

        if (pago.Recibo is not null)
        {
            pago.Recibo.Estado = "completado";
            pago.Recibo.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (pago.Reserva is not null && pago.Reserva.Estado != "cancelada")
        {
            if (pago.TipoPago == "saldo_50")
            {
                pago.Reserva.SaldoAbonado = true;
                pago.Reserva.SaldoPendiente = 0m;
                pago.Reserva.Estado = "finalizada";
            }
            else
            {
                pago.Reserva.Estado = "confirmada";
            }
            pago.Reserva.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            received = true,
            matched = true,
            paymentId = pago.Id,
            mpPaymentId = pago.MpPaymentId,
            mpPreferenceId = pago.MpPreferenceId,
            status = "aprobado",
            internalStatus = pago.Estado,
            reservationId = pago.ReservaId,
            reservationStatus = pago.Reserva?.Estado
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

    private async Task<Cliente> ResolveOrCreateClienteAsync(
        string dni,
        string fullName,
        CancellationToken cancellationToken)
    {
        var normalizedDni = string.IsNullOrWhiteSpace(dni) ? "00.000.000" : dni.Trim();
        var existing = await _dbContext.Clientes
            .FirstOrDefaultAsync(c => c.Dni == normalizedDni, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var existingUsuario = await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Dni == normalizedDni, cancellationToken);

        if (existingUsuario is not null)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO public.clientes (id, numero_socio, fecha_alta_socio, total_reservas, saldo_credito_favor) VALUES ({existingUsuario.Id}, {$"TA-{existingUsuario.Id}"}, CURRENT_DATE, 0, 0) ON CONFLICT (id) DO NOTHING",
                cancellationToken);
            return await _dbContext.Clientes.FirstAsync(c => c.Id == existingUsuario.Id, cancellationToken);
        }

        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var nombre = parts.Length > 0 ? parts[0] : "Socio";
        var apellido = parts.Length > 1 ? parts[1] : "Tenis Ahora";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nuevo = new Cliente
        {
            Id = $"usr-{nowMs}",
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = fullName,
            Dni = normalizedDni,
            Email = $"socio.{nowMs}@tenisahora.com",
            Rol = "client",
            Activo = true,
            NumeroSocio = $"TA-{1000 + (nowMs % 9000)}",
            FechaAltaSocio = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _dbContext.Clientes.Add(nuevo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return nuevo;
    }

    private static string NormalizePaymentMethod(string raw)
    {
        var trimmed = raw.Trim();
        foreach (var allowed in AllowedPaymentMethods)
        {
            if (string.Equals(allowed, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }
        return "Mercado Pago (QR)";
    }

    private static string DeterminePaymentType(string concept)
    {
        var lower = concept.ToLowerInvariant();
        if (lower.Contains("saldo")) return "saldo_50";
        if (lower.Contains("inscripc") || lower.Contains("torneo") || lower.Contains("liga")) return "inscripcion";
        if (lower.Contains("cuota") || lower.Contains("escuela") || lower.Contains("clase")) return "cuota_clase";
        if (lower.Contains("particular")) return "turno_particular";
        if (lower.Contains("cargo") || lower.Contains("retención") || lower.Contains("retencion")) return "cargo_cancelacion";
        return "sena_50";
    }

    private static object MapReciboDto(Recibo r)
    {
        object items;
        try
        {
            items = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(r.ItemsJson) ? "[]" : r.ItemsJson);
        }
        catch
        {
            items = Array.Empty<object>();
        }

        return new
        {
            id = r.Id,
            reservationId = r.ReservaId,
            clientName = r.ClienteNombre,
            clientDni = r.ClienteDni,
            concept = r.Concepto,
            items,
            totalPaid = r.TotalAbonado,
            paymentMethod = r.MedioPago,
            status = r.Estado,
            date = r.FechaEmision.ToString("yyyy-MM-dd HH:mm"),
            qrData = r.QrData ?? $"https://mpago.la/pos/tenisahora/{r.Id.ToLowerInvariant()}",
            cashierName = r.CajeroNombre
        };
    }

    private static object MapDescuentoDto(Descuento d) => new
    {
        id = d.Id,
        name = d.Nombre,
        discountPercent = d.PorcentajeDescuento,
        code = d.Codigo,
        applicableTo = d.RubroAplicable,
        active = d.Activo
    };

    private static object MapFacturaDto(Factura f)
    {
        object items;
        try
        {
            items = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(f.ItemsJson) ? "[]" : f.ItemsJson);
        }
        catch
        {
            items = Array.Empty<object>();
        }

        return new
        {
            id = f.Id,
            clientName = f.ClienteNombre,
            clientDni = f.ClienteDniOCuit,
            clientAddress = f.ClienteDireccion ?? string.Empty,
            concept = f.Concepto,
            items,
            subtotal = f.Subtotal,
            total = f.Total,
            paymentMethod = f.MedioPago,
            relatedReceiptId = f.ReciboRelacionadoId,
            status = f.Estado,
            date = f.FechaEmision.ToString("yyyy-MM-dd HH:mm"),
            issuedBy = f.EmitidaPor
        };
    }

    private static object MapReembolsoDto(Reembolso r) => new
    {
        id = r.Id,
        clientName = r.ClienteNombre,
        clientDni = r.ClienteDni,
        relatedReservationId = r.ReservaRelacionadaId,
        relatedReceiptId = r.ReciboRelacionadoId,
        reason = r.Motivo,
        refundType = r.TipoReembolso,
        originalAmount = r.MontoOriginal,
        refundAmount = r.MontoReembolsado,
        refundMethod = r.MedioDevolucion,
        date = r.FechaReembolso.ToString("yyyy-MM-dd HH:mm"),
        processedBy = r.ProcesadoPor
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

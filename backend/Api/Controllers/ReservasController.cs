using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenisAhora.Application.DTOs;
using TenisAhora.Application.Interfaces;
using TenisAhora.Infrastructure.Authentication;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Controlador REST para la gestión de reservas de canchas, disponibilidad de turnos,
/// cobro de señas 50%, liquidación de saldo 50% y cancelaciones (IEEE SRS-830).
/// </summary>
[ApiController]
[Route("api/reservas")]
[Authorize]
public class ReservasController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservasController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// GET /api/reservas — El personal (admin) visualiza todas las reservas; el socio (client) solo las propias.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = SupabaseAuthenticationExtensions.ClientOrStaffPolicy)]
    public async Task<ActionResult<IReadOnlyList<ReservationResponseDto>>> GetReservations(
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.GetReservationsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/reservas/ocupacion — Turnos ocupados de reservas activas sin exponer datos sensibles de socios.
    /// </summary>
    [HttpGet("ocupacion")]
    [Authorize(Policy = SupabaseAuthenticationExtensions.ClientOrStaffPolicy)]
    public async Task<ActionResult<IReadOnlyList<ReservationOccupancyDto>>> GetOccupancy(
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.GetOccupancyAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/reservas — Ejecuta la transacción atómica:
    /// 1) Verifica disponibilidad, máximo 2h y antelación &lt;= 30 días,
    /// 2) Descuenta stock de materiales deportivos (bloqueando si stock == 0),
    /// 3) Genera la preferencia de pago en Mercado Pago por la seña obligatoria del 50%.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = SupabaseAuthenticationExtensions.ClientOrStaffPolicy)]
    public async Task<ActionResult<CreateReservationResultDto>> CreateReservation(
        [FromBody] CreateReservationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.CreateReservationAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// PUT /api/reservas/{id} — Modifica y recalcula una reserva existente.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = SupabaseAuthenticationExtensions.AdminPolicy)]
    public async Task<ActionResult<ReservationResponseDto>> UpdateReservation(
        [FromRoute] string id,
        [FromBody] UpdateReservationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.UpdateReservationAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/reservas/{id}/pagar-saldo — Liquida el 50% restante al finalizar el turno y restituye el stock.
    /// </summary>
    [HttpPost("{id}/pagar-saldo")]
    [Authorize(Policy = SupabaseAuthenticationExtensions.AdminPolicy)]
    public async Task<ActionResult<ReservationResponseDto>> PayRemainingBalance(
        [FromRoute] string id,
        [FromBody] PayRemainingRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.PayRemainingBalanceAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/reservas/{id}/cancelar — Cancela una reserva aplicando las políticas RD02-RD04 y restituyendo stock.
    /// </summary>
    [HttpPost("{id}/cancelar")]
    [Authorize(Policy = SupabaseAuthenticationExtensions.ClientOrStaffPolicy)]
    public async Task<ActionResult<CancelReservationResponseDto>> CancelReservation(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var result = await _reservationService.CancelReservationAsync(id, cancellationToken);
        return Ok(result);
    }
}

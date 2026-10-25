using TenisAhora.Application.DTOs;

namespace TenisAhora.Application.Interfaces;

/// <summary>
/// Contrato del servicio de aplicación para la gestión integral de reservas de canchas (IEEE SRS-830).
/// </summary>
public interface IReservationService
{
    Task<CreateReservationResultDto> CreateReservationAsync(
        CreateReservationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationResponseDto>> GetReservationsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationOccupancyDto>> GetOccupancyAsync(
        CancellationToken cancellationToken = default);

    Task<ReservationResponseDto> UpdateReservationAsync(
        string id,
        UpdateReservationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ReservationResponseDto> PayRemainingBalanceAsync(
        string id,
        PayRemainingRequestDto request,
        CancellationToken cancellationToken = default);

    Task<CancelReservationResponseDto> CancelReservationAsync(
        string id,
        CancellationToken cancellationToken = default);
}

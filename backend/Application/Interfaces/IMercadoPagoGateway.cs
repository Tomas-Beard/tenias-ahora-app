using TenisAhora.Application.DTOs;

namespace TenisAhora.Application.Interfaces;

/// <summary>
/// Contrato de pasarela de pago que aísla completamente el SDK de Mercado Pago (Sandbox)
/// de la lógica de aplicación.
/// </summary>
public interface IMercadoPagoGateway
{
    /// <summary>
    /// Crea una preferencia de pago en Mercado Pago por la seña obligatoria del 50% (RD01 / RD11).
    /// </summary>
    Task<PaymentPreferenceResultDto> CreateDepositPreferenceAsync(
        PaymentPreferenceRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una preferencia de pago / orden QR por el 50% de saldo restante al finalizar la reserva.
    /// </summary>
    Task<PaymentPreferenceResultDto> CreateRemainingBalancePreferenceAsync(
        PaymentPreferenceRequestDto request,
        CancellationToken cancellationToken = default);
}

namespace TenisAhora.Application.DTOs;

public class CreateReservationResultDto
{
    public ReservationResponseDto Data { get; set; } = new();
    public ReceiptResponseDto Receipt { get; set; } = new();
    public PaymentPreferenceResultDto PaymentPreference { get; set; } = new();
}

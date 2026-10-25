namespace TenisAhora.Application.DTOs;

public class CancelReservationResponseDto
{
    public ReservationResponseDto Data { get; set; } = new();
    public bool IsWithinFreeWindow { get; set; }
    public RefundResponseDto Refund { get; set; } = new();
    public ReceiptResponseDto? PenaltyReceipt { get; set; }
}

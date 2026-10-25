namespace TenisAhora.Application.DTOs;

public class ReceiptResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string? ReservationId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientDni { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public List<ReceiptItemDto> Items { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "completado";
    public string Date { get; set; } = string.Empty;
    public string? QrData { get; set; }
    public string? PaymentUrl { get; set; }
    public string? MpPreferenceId { get; set; }
    public string CashierName { get; set; } = "Cobro Digital Online";
}

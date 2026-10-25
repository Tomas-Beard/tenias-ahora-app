namespace TenisAhora.Application.DTOs;

public class RefundResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientDni { get; set; } = string.Empty;
    public string RelatedReservationId { get; set; } = string.Empty;
    public string? RelatedReceiptId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RefundType { get; set; } = "total"; // 'total' | 'parcial'
    public decimal OriginalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string ProcessedBy { get; set; } = "Sistema de Reservas";
}

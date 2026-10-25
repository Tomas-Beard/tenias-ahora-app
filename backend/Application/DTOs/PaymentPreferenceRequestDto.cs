namespace TenisAhora.Application.DTOs;

public class PaymentPreferenceRequestDto
{
    public string ExternalReference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string PayerDni { get; set; } = string.Empty;
    public string PayerEmail { get; set; } = string.Empty;
}

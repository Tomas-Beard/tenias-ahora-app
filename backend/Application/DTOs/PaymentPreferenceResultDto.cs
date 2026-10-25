namespace TenisAhora.Application.DTOs;

public class PaymentPreferenceResultDto
{
    public string PreferenceId { get; set; } = string.Empty;
    public string InitPoint { get; set; } = string.Empty;
    public string SandboxInitPoint { get; set; } = string.Empty;
    public string QrData { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}

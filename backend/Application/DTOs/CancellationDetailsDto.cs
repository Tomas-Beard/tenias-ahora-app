namespace TenisAhora.Application.DTOs;

public class CancellationDetailsDto
{
    public string CancelledAt { get; set; } = string.Empty;
    public double HoursInAdvance { get; set; }
    public string PolicyApplied { get; set; } = string.Empty;
}

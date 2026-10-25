namespace TenisAhora.Application.DTOs;

public class ReservationParticipantDto
{
    public string Name { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsLead { get; set; }
}

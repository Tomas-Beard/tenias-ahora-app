namespace TenisAhora.Application.DTOs;

public class ReservationOccupancyDto
{
    public string Id { get; set; } = string.Empty;
    public string CourtId { get; set; } = string.Empty;
    public string CourtName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public decimal DurationHours { get; set; }
    public string Status { get; set; } = string.Empty;
}

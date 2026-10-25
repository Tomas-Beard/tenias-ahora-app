namespace TenisAhora.Application.DTOs;

public class CreateReservationRequestDto
{
    public string CourtId { get; set; } = string.Empty;
    public string? CourtName { get; set; }
    public string? SurfaceType { get; set; }
    public string Date { get; set; } = string.Empty; // Formato YYYY-MM-DD
    public string StartTime { get; set; } = string.Empty; // Formato HH:mm
    public string? EndTime { get; set; }
    public decimal DurationHours { get; set; } = 1.0m; // Máximo 2.0h (IEEE SRS-830)
    public string PlayersType { get; set; } = "singles"; // 'singles' | 'dobles'
    public List<ReservationParticipantDto> Participants { get; set; } = new();
    public EquipmentAssignedDto? EquipmentAssigned { get; set; }
    public decimal? CourtCost { get; set; }
    public decimal EquipmentCost { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? DepositPaid { get; set; }
    public string DepositPaymentMethod { get; set; } = "Mercado Pago (QR)";
}

namespace TenisAhora.Application.DTOs;

public class ReservationResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string CourtId { get; set; } = string.Empty;
    public string CourtName { get; set; } = string.Empty;
    public string SurfaceType { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public decimal DurationHours { get; set; }
    public string PlayersType { get; set; } = "singles";
    public List<ReservationParticipantDto> Participants { get; set; } = new();
    public EquipmentAssignedDto EquipmentAssigned { get; set; } = new();
    public decimal CourtCost { get; set; }
    public decimal EquipmentCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal DepositPaid { get; set; }
    public string? DepositReceiptNumber { get; set; }
    public string? DepositPaymentMethod { get; set; }
    public decimal RemainingBalance { get; set; }
    public bool RemainingPaid { get; set; }
    public string? FinalReceiptNumber { get; set; }
    public string Status { get; set; } = "confirmada";
    public string CreatedAt { get; set; } = string.Empty;
    public string? UpdatedAt { get; set; }
    public CancellationDetailsDto? CancellationDetails { get; set; }
}

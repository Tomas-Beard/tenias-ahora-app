namespace TenisAhora.Application.DTOs;

public class UpdateReservationRequestDto
{
    public string? CourtId { get; set; }
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public decimal? DurationHours { get; set; }
    public string? PlayersType { get; set; }
    public List<ReservationParticipantDto>? Participants { get; set; }
    public EquipmentAssignedDto? EquipmentAssigned { get; set; }
    public decimal? EquipmentCost { get; set; }
    public string? Status { get; set; }
}

namespace TenisAhora.Application.DTOs;

public class ClubConfigOptions
{
    public const string SectionName = "ClubConfig";

    public double FreeCancellationHours { get; set; } = 6.0;
    public decimal LateCancellationRetentionPercent { get; set; } = 50.0m;
    public int MaxAdvanceBookingDays { get; set; } = 30;
    public decimal PrivateLessonHourlyRate { get; set; } = 6000.0m;
    public bool UseApplicationLayerStockManagement { get; set; } = true;
}

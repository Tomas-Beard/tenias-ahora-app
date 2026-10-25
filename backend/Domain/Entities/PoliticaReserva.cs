namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Política parametrizada del club para reservas y cancelaciones (RD01 a RD05).
/// Mapeada a <c>public.politicas_reserva</c> en Supabase PostgreSQL.
/// </summary>
public class PoliticaReserva
{
    public string Id { get; set; } = string.Empty;
    public string NombrePolitica { get; set; } = string.Empty;
    public decimal DuracionMaximaHoras { get; set; } = 2.00m;
    public int DiasMaxAntelacion { get; set; } = 30;
    public decimal PorcentajeSenaObligatoria { get; set; } = 50.00m;
    public decimal HorasCancelacionGratuita { get; set; } = 6.00m;
    public decimal PorcentajeRetencionCancelacionTardia { get; set; } = 50.00m;
    public bool BloqueoEstrictoStockCero { get; set; } = true;
    public bool Activa { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}

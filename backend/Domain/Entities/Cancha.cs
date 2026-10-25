namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 2 — Cancha de tenis del club (RF006-RF010, RD06).
/// Mapeada a <c>public.canchas</c> en Supabase PostgreSQL.
/// </summary>
public class Cancha
{
    public string Id { get; set; } = string.Empty;
    public string TipoCanchaId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Superficie { get; set; } = string.Empty;
    public int CapacidadJugadores { get; set; } = 4;
    public decimal PrecioPorHora { get; set; }
    public string Estado { get; set; } = "disponible"; // 'disponible' | 'ocupada' | 'mantenimiento'
    public string? Descripcion { get; set; }
    public string? NotasMantenimiento { get; set; }
    public string? FotoUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TipoCancha? TipoCancha { get; set; }
    public ICollection<HorarioCancha> HorariosCancha { get; set; } = new List<HorarioCancha>();
    public ICollection<BloqueoMantenimiento> BloqueosMantenimiento { get; set; } = new List<BloqueoMantenimiento>();
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    public ICollection<Enfrentamiento> Enfrentamientos { get; set; } = new List<Enfrentamiento>();
    public ICollection<ActividadGrupal> ActividadesGrupales { get; set; } = new List<ActividadGrupal>();
    public ICollection<TurnoParticular> TurnosParticulares { get; set; } = new List<TurnoParticular>();
}

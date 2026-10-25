namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Turno particular de clase o entrenamiento solicitado a un <see cref="Profesional"/> (RF075-RF078, RF088-RF091).
/// Mapeado a <c>public.turnos_particulares</c> en Supabase PostgreSQL.
/// </summary>
public class TurnoParticular
{
    public string Id { get; set; } = string.Empty;
    public string ProfesionalId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? CanchaId { get; set; }
    public string Modalidad { get; set; } = "clase"; // 'clase' | 'entrenamiento'
    public string NombreProfesional { get; set; } = string.Empty;
    public string NombreAlumno { get; set; } = string.Empty;
    public string DniAlumno { get; set; } = string.Empty;
    public string? TelefonoAlumno { get; set; }
    public string? CanchaAsignada { get; set; }
    public DateOnly Fecha { get; set; }
    public string FranjaHoraria { get; set; } = "10:00 - 11:00";
    public TimeOnly HoraInicio { get; set; } = new(10, 0);
    public TimeOnly HoraFin { get; set; } = new(11, 0);
    public decimal TarifaHora { get; set; } = 6000.00m;
    public string Estado { get; set; } = "programada"; // 'programada' | 'confirmada' | 'cancelada' | 'finalizada'
    public string? Notas { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Profesional? Profesional { get; set; }
    public Cliente? Cliente { get; set; }
    public Cancha? Cancha { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

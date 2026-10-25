namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Entidad base de la jerarquía TPT (Table-Per-Type) de actividades grupales (RD07-RD10).
/// Mapeada a <c>public.actividades_grupales</c> en Supabase PostgreSQL.
/// </summary>
public class ActividadGrupal
{
    public string Id { get; set; } = string.Empty;
    public string ProfesionalId { get; set; } = string.Empty;
    public string? CanchaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = "clase"; // 'clase' | 'entrenamiento'
    public string NombreProfesional { get; set; } = string.Empty;
    public string? CanchaAsignada { get; set; }
    public string DiasSemana { get; set; } = string.Empty;
    public string HorarioFranja { get; set; } = string.Empty;
    public int CupoMaximo { get; set; } = 30;
    public int InscriptosActuales { get; set; }
    public decimal ArancelMensual { get; set; }
    public string? PeriodoMes { get; set; }
    public string Estado { get; set; } = "activa"; // 'activa' | 'finalizada' | 'suspendida'
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Profesional? Profesional { get; set; }
    public Cancha? Cancha { get; set; }
    public ICollection<InscripcionActividad> Inscripciones { get; set; } = new List<InscripcionActividad>();
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
}

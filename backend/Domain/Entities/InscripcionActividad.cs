namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Inscripción de un alumno (<see cref="Cliente"/>) a una <see cref="ActividadGrupal"/> (RF071-RF074, RF084-RF087).
/// Mapeada a <c>public.inscripciones_actividad</c> en Supabase PostgreSQL.
/// </summary>
public class InscripcionActividad
{
    public string Id { get; set; } = string.Empty;
    public string ActividadGrupalId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string NombreAlumno { get; set; } = string.Empty;
    public string DniAlumno { get; set; } = string.Empty;
    public string? TelefonoAlumno { get; set; }
    public DateOnly FechaInscripcion { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Estado { get; set; } = "activa"; // 'activa' | 'baja' | 'finalizada'
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ActividadGrupal? ActividadGrupal { get; set; }
    public Cliente? Cliente { get; set; }
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

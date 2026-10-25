namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Registro de asistencia de un alumno a una sesión de una <see cref="ActividadGrupal"/> (RF092-RF099).
/// Mapeado a <c>public.asistencias</c> en Supabase PostgreSQL.
/// </summary>
public class Asistencia
{
    public string Id { get; set; } = string.Empty;
    public string ActividadGrupalId { get; set; } = string.Empty;
    public string InscripcionActividadId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public int NumeroSesion { get; set; }
    public string EtiquetaSesion { get; set; } = string.Empty;
    public DateOnly FechaSesion { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string EstadoAsistencia { get; set; } = "P"; // 'P' | 'A' | 'J' | '-'
    public string? Observaciones { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ActividadGrupal? ActividadGrupal { get; set; }
    public InscripcionActividad? InscripcionActividad { get; set; }
    public Cliente? Cliente { get; set; }
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 2 — Bloqueo programado por mantenimiento o evento en una cancha (RD06).
/// Mapeado a <c>public.bloqueos_mantenimiento</c> en Supabase PostgreSQL.
/// </summary>
public class BloqueoMantenimiento
{
    public string Id { get; set; } = string.Empty;
    public string CanchaId { get; set; } = string.Empty;
    public string? DiaSemana { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public string? CreadoPorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cancha? Cancha { get; set; }
    public Administrador? CreadoPor { get; set; }
}

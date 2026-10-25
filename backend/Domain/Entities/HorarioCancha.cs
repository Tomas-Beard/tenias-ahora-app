namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 2 — Horario operativo de una cancha por día de la semana (RF016-RF019).
/// Mapeado a <c>public.horarios_cancha</c> en Supabase PostgreSQL.
/// </summary>
public class HorarioCancha
{
    public string Id { get; set; } = string.Empty;
    public string CanchaId { get; set; } = string.Empty;
    public string DiaSemana { get; set; } = "Lunes";
    public TimeOnly HoraApertura { get; set; } = new(8, 0);
    public TimeOnly HoraCierre { get; set; } = new(22, 0);
    public int DuracionTurnoMinutos { get; set; } = 60;
    public bool Habilitado { get; set; } = true;
    public string? Notas { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cancha? Cancha { get; set; }
}

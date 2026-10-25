namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Franja de disponibilidad horaria semanal de un <see cref="Profesional"/> (RF104-RF107, RF112-RF115).
/// Mapeado a <c>public.horarios_profesional</c> en Supabase PostgreSQL.
/// </summary>
public class HorarioProfesional
{
    public string Id { get; set; } = string.Empty;
    public string ProfesionalId { get; set; } = string.Empty;
    public string DiaSemana { get; set; } = "Lunes";
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool Disponible { get; set; } = true;
    public string? Observaciones { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Profesional? Profesional { get; set; }
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Inscripción de un socio (<see cref="Cliente"/>) a una <see cref="Competencia"/> (RF049-RF056).
/// Mapeada a <c>public.inscripciones_competencia</c> en Supabase PostgreSQL.
/// </summary>
public class InscripcionCompetencia
{
    public string Id { get; set; } = string.Empty;
    public string CompetenciaId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? EquipoId { get; set; }
    public string NombreParticipante { get; set; } = string.Empty;
    public string DniParticipante { get; set; } = string.Empty;
    public string? TelefonoParticipante { get; set; }
    public string? NombrePareja { get; set; }
    public string? DniPareja { get; set; }
    public DateOnly FechaInscripcion { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal MontoInscripcion { get; set; }
    public string Estado { get; set; } = "confirmada"; // 'pendiente_pago' | 'confirmada' | 'baja'
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Competencia? Competencia { get; set; }
    public Cliente? Cliente { get; set; }
    public Equipo? Equipo { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

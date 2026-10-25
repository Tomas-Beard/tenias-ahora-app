namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Competencia deportiva (Torneo o Liga) vinculada a su tipo y reglamento (RF035-RF048, RD12).
/// Mapeada a <c>public.competencias</c> en Supabase PostgreSQL.
/// </summary>
public class Competencia
{
    public string Id { get; set; } = string.Empty;
    public string TipoCompetenciaId { get; set; } = string.Empty;
    public string ReglamentoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ClaseCompetencia { get; set; } = "Torneo"; // 'Torneo' | 'Liga'
    public string Categoria { get; set; } = string.Empty;
    public string Modalidad { get; set; } = "Singles"; // 'Singles' | 'Dobles'
    public string? SuperficieRequerida { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal CostoInscripcion { get; set; }
    public int MaxParticipantes { get; set; } = 16;
    public int InscriptosActuales { get; set; }
    public string Estado { get; set; } = "inscripcion_abierta"; // 'inscripcion_abierta' | 'en_curso' | 'finalizado' | 'cancelado'
    public string? Descripcion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TipoCompetencia? TipoCompetencia { get; set; }
    public Reglamento? Reglamento { get; set; }
    public ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
    public ICollection<InscripcionCompetencia> Inscripciones { get; set; } = new List<InscripcionCompetencia>();
    public ICollection<Enfrentamiento> Enfrentamientos { get; set; } = new List<Enfrentamiento>();
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Formato y sistema de puntuación de una competencia (RF061-RF065).
/// Mapeado a <c>public.tipos_competencia</c> en Supabase PostgreSQL.
/// </summary>
public class TipoCompetencia
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Formato { get; set; } = string.Empty;
    public string AplicaA { get; set; } = "Torneo"; // 'Torneo' | 'Liga' | 'Ambos'
    public string? SistemaPuntuacion { get; set; }
    public int MinParticipantes { get; set; } = 2;
    public int MaxParticipantes { get; set; } = 32;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Competencia> Competencias { get; set; } = new List<Competencia>();
}

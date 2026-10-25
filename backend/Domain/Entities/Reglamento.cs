namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Reglamento interno obligatorio y documento normativo de una competencia (RF041, RF048, RD12).
/// Mapeado a <c>public.reglamentos</c> en Supabase PostgreSQL.
/// </summary>
public class Reglamento
{
    public string Id { get; set; } = string.Empty;
    public string CodigoReglamento { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string NormativaBaseAsociacion { get; set; } = "Reglas Oficiales Asociación Argentina de Tenis (AAT)";
    public string TextoReglamentoInterno { get; set; } = string.Empty;
    public string? DocumentoNombre { get; set; }
    public string? DocumentoMimeType { get; set; }
    public int? DocumentoTamanoBytes { get; set; }
    public string? DocumentoBase64OUrl { get; set; }
    public DateOnly FechaVigencia { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Competencia> Competencias { get; set; } = new List<Competencia>();
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Certificado o título oficial habilitante AAT de un <see cref="Profesional"/> (RF116-RF124, RD08, RD09).
/// Mapeado a <c>public.certificados</c> en Supabase PostgreSQL.
/// </summary>
public class Certificado
{
    public string Id { get; set; } = string.Empty;
    public string ProfesionalId { get; set; } = string.Empty;
    public string TituloHabilitante { get; set; } = string.Empty;
    public string InstitucionEmisora { get; set; } = "Asociación Argentina de Tenis (AAT)";
    public string NumeroMatricula { get; set; } = string.Empty;
    public DateOnly FechaEmision { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = "habilitado"; // 'habilitado' | 'vencido' | 'suspendido' | 'pendiente'
    public string? DocumentoNombre { get; set; }
    public string? DocumentoMimeType { get; set; }
    public int? DocumentoTamanoBytes { get; set; }
    public string? DocumentoUrlOBase64 { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Profesional? Profesional { get; set; }
}

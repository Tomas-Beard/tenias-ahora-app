namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 2 — Tipo de superficie de cancha y tope de alumnos por clase (RF011-RF015, RD07, RD10).
/// Mapeado a <c>public.tipos_cancha</c> en Supabase PostgreSQL.
/// </summary>
public class TipoCancha
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? VelocidadSuperficie { get; set; }
    public decimal PrecioBaseHora { get; set; }
    public string? FrecuenciaMantenimiento { get; set; }
    public int MaxAlumnosPorClase { get; set; } = 30;
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Cancha> Canchas { get; set; } = new List<Cancha>();
}

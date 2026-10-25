namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Subclase TPT que hereda de <see cref="ActividadGrupal"/> y representa una Clase grupal de escuela (RF066-RF070).
/// Mapeada a <c>public.clases</c> en Supabase PostgreSQL.
/// </summary>
public class Clase : ActividadGrupal
{
    public string NivelEscuela { get; set; } = "Intermedio";
    public string RangoEdad { get; set; } = "Adultos";
    public string? ObjetivoPedagogico { get; set; }
    public bool IncluyeMaterialesEscuela { get; set; } = true;
}

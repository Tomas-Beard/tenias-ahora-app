namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 5 — Subclase TPT que hereda de <see cref="ActividadGrupal"/> y representa un Entrenamiento físico-técnico (RF079-RF083).
/// Mapeado a <c>public.entrenamientos</c> en Supabase PostgreSQL.
/// </summary>
public class Entrenamiento : ActividadGrupal
{
    public string EnfoqueEntrenamiento { get; set; } = "Físico-Técnico";
    public string NivelIntensidad { get; set; } = "Alta";
    public string? PlanFisicoAsociado { get; set; }
    public bool RequiereAptoMedicoCompetitivo { get; set; } = true;
}

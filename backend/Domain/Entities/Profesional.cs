namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Subclase TPT que hereda de <see cref="Usuario"/> y representa al Profesor o Entrenador habilitado.
/// Mapeada a <c>public.profesionales</c> en Supabase PostgreSQL (RD08 / RD09).
/// </summary>
public class Profesional : Usuario
{
    public string TipoProfesional { get; set; } = "Profesor"; // 'Profesor' | 'Entrenador'
    public string? Especialidad { get; set; }
    public int AniosExperiencia { get; set; }
    public bool PoseeLicenciaOficial { get; set; } = true;
    public decimal TarifaHoraParticular { get; set; } = 6000.00m;
    public string? Biografia { get; set; }

    public ICollection<Certificado> Certificados { get; set; } = new List<Certificado>();
    public ICollection<HorarioProfesional> HorariosDisponibles { get; set; } = new List<HorarioProfesional>();
    public ICollection<ActividadGrupal> ActividadesGrupales { get; set; } = new List<ActividadGrupal>();
    public ICollection<TurnoParticular> TurnosParticulares { get; set; } = new List<TurnoParticular>();
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Subclase TPT que hereda de <see cref="Usuario"/> y representa al personal administrativo.
/// Mapeada a <c>public.administradores</c> en Supabase PostgreSQL.
/// </summary>
public class Administrador : Usuario
{
    public string LegajoEmpleado { get; set; } = string.Empty;
    public string Cargo { get; set; } = "Administración General";
    public string Departamento { get; set; } = "Recepción y Caja";
    public int NivelAcceso { get; set; } = 1;
    public DateOnly FechaIngreso { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public ICollection<BloqueoMantenimiento> BloqueosCreados { get; set; } = new List<BloqueoMantenimiento>();
}

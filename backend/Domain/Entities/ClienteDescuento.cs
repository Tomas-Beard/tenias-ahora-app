namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Entidad de unión N:M entre <see cref="Cliente"/> y <see cref="Descuento"/>.
/// Mapeada a <c>public.cliente_descuentos</c> en Supabase PostgreSQL.
/// </summary>
public class ClienteDescuento
{
    public string ClienteId { get; set; } = string.Empty;
    public string DescuentoId { get; set; } = string.Empty;
    public DateTimeOffset FechaAsignacion { get; set; } = DateTimeOffset.UtcNow;

    public Cliente? Cliente { get; set; }
    public Descuento? Descuento { get; set; }
}

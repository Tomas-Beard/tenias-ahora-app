namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Descuento promocional o beneficio del club para socios (RF138-RF142).
/// Mapeado a <c>public.descuentos</c> en Supabase PostgreSQL.
/// </summary>
public class Descuento
{
    public string Id { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal PorcentajeDescuento { get; set; }
    public string RubroAplicable { get; set; } = "Alquiler de Canchas";
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ClienteDescuento> ClienteDescuentos { get; set; } = new List<ClienteDescuento>();
    public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
}

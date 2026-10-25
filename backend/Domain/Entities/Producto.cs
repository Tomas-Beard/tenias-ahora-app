namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Producto o material deportivo del club (redes, pelotas, raquetas, accesorios — RF030-RF034).
/// Mapeado a <c>public.productos</c> en Supabase PostgreSQL.
/// </summary>
public class Producto
{
    public string Id { get; set; } = string.Empty;
    public string CodigoSku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Equipamiento"; // 'Redes' | 'Pelotas' | 'Raquetas' | 'Accesorios' | 'Equipamiento'
    public string? Descripcion { get; set; }
    public string UnidadMedida { get; set; } = "unidades";
    public decimal PrecioUnitarioAlquilerOVenta { get; set; }
    public bool EsObligatorioEnReserva { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Stock? Stock { get; set; }
    public ICollection<AsignacionEquipamiento> AsignacionesEquipamiento { get; set; } = new List<AsignacionEquipamiento>();
}

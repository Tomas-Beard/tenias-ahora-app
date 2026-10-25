namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Stock e inventario físico en tiempo real de un <see cref="Producto"/> (RF026-RF029).
/// Mapeado a <c>public.stock</c> en Supabase PostgreSQL.
/// </summary>
public class Stock
{
    public string Id { get; set; } = string.Empty;
    public string ProductoId { get; set; } = string.Empty;
    public int StockTotal { get; set; }
    public int StockEnUso { get; set; }
    public int StockDisponible { get; set; }
    public int UmbralMinimoAlerta { get; set; } = 2;
    public string Estado { get; set; } = "disponible"; // 'disponible' | 'alerta_baja' | 'agotado'
    public DateTimeOffset UltimaActualizacion { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Producto? Producto { get; set; }
    public ICollection<AsignacionEquipamiento> AsignacionesEquipamiento { get; set; } = new List<AsignacionEquipamiento>();
}

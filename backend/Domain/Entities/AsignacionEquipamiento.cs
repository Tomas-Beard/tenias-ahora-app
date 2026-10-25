namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Detalle relacional del equipamiento asignado a una <see cref="Reserva"/> (redes, pelotas, raquetas).
/// Mapeado a <c>public.asignaciones_equipamiento</c> en Supabase PostgreSQL.
/// </summary>
public class AsignacionEquipamiento
{
    public string Id { get; set; } = string.Empty;
    public string ReservaId { get; set; } = string.Empty;
    public string ProductoId { get; set; } = string.Empty;
    public string StockId { get; set; } = string.Empty;
    public int CantidadAsignada { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public string EstadoDevolucion { get; set; } = "en_uso"; // 'en_uso' | 'devuelto' | 'extraviado'
    public DateTimeOffset FechaAsignacion { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FechaRestitucion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Reserva? Reserva { get; set; }
    public Producto? Producto { get; set; }
    public Stock? Stock { get; set; }
}

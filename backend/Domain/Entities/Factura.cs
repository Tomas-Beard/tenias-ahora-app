namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Factura fiscal emitida por el club (RF125-RF127).
/// Mapeada a <c>public.facturas</c> en Supabase PostgreSQL.
/// </summary>
public class Factura
{
    public string Id { get; set; } = string.Empty;
    public string NumeroFactura { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? ReciboRelacionadoId { get; set; }
    public string? PagoId { get; set; }
    public string TipoComprobante { get; set; } = "B"; // 'A' | 'B' | 'C'
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDniOCuit { get; set; } = string.Empty;
    public string? ClienteDireccion { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public decimal Subtotal { get; set; }
    public decimal AlicuotaIva { get; set; } = 21.00m;
    public decimal MontoIva { get; set; }
    public decimal Total { get; set; }
    public string MedioPago { get; set; } = string.Empty;
    public string Estado { get; set; } = "emitida"; // 'emitida' | 'anulada'
    public string EmitidaPor { get; set; } = "Administración General";
    public DateTimeOffset FechaEmision { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cliente? Cliente { get; set; }
    public Recibo? ReciboRelacionado { get; set; }
    public Pago? Pago { get; set; }
}

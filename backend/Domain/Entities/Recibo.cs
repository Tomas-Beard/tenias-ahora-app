namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Comprobante/recibo oficial de pago emitido al socio (RF134-RF137).
/// Mapeado a <c>public.recibos</c> en Supabase PostgreSQL.
/// </summary>
public class Recibo
{
    public string Id { get; set; } = string.Empty;
    public string NumeroRecibo { get; set; } = string.Empty;
    public string PagoId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? ReservaId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDni { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public decimal TotalAbonado { get; set; }
    public string MedioPago { get; set; } = "Mercado Pago (QR)";
    public string Estado { get; set; } = "completado"; // 'completado' | 'anulado'
    public string? QrData { get; set; }
    public string CajeroNombre { get; set; } = "Cobro Digital Online";
    public DateTimeOffset FechaEmision { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Pago? Pago { get; set; }
    public Cliente? Cliente { get; set; }
    public Reserva? Reserva { get; set; }
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public ICollection<Reembolso> Reembolsos { get; set; } = new List<Reembolso>();
}

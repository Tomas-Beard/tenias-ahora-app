namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Reembolso total o parcial asociado a una cancelación de reserva (RF131-RF133, RD02-RD04).
/// Mapeado a <c>public.reembolsos</c> en Supabase PostgreSQL.
/// </summary>
public class Reembolso
{
    public string Id { get; set; } = string.Empty;
    public string NumeroReembolso { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? ReservaRelacionadaId { get; set; }
    public string? ReciboRelacionadoId { get; set; }
    public string? PagoRelacionadoId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDni { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string TipoReembolso { get; set; } = "total"; // 'total' | 'parcial'
    public decimal MontoOriginal { get; set; }
    public decimal MontoReembolsado { get; set; }
    public string MedioDevolucion { get; set; } = string.Empty;
    public string Estado { get; set; } = "procesado"; // 'pendiente' | 'procesado' | 'rechazado'
    public string ProcesadoPor { get; set; } = "Sistema de Reservas";
    public DateTimeOffset FechaReembolso { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cliente? Cliente { get; set; }
    public Reserva? ReservaRelacionada { get; set; }
    public Recibo? ReciboRelacionado { get; set; }
    public Pago? PagoRelacionado { get; set; }
}

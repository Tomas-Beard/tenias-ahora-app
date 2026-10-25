namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 6 — Transacción de pago (seña 50%, saldo 50%, inscripción, cuota o cargo) e integración con Mercado Pago (RF128-RF130, RD01, RD11).
/// Mapeado a <c>public.pagos</c> en Supabase PostgreSQL.
/// </summary>
public class Pago
{
    public string Id { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string? ReservaId { get; set; }
    public string? InscripcionCompetenciaId { get; set; }
    public string? InscripcionActividadId { get; set; }
    public string? TurnoParticularId { get; set; }
    public string TipoPago { get; set; } = "sena_50"; // 'sena_50' | 'saldo_50' | 'inscripcion' | 'cuota_clase' | 'turno_particular' | 'cargo_cancelacion'
    public string MedioPago { get; set; } = "Mercado Pago (QR)";
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "completado"; // 'pendiente' | 'completado' | 'rechazado' | 'reembolsado'
    public string? MpPreferenceId { get; set; }
    public string? MpPaymentId { get; set; }
    public string? MpStatus { get; set; }
    public string? QrData { get; set; }
    public string? PaymentUrl { get; set; }
    public DateTimeOffset FechaPago { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cliente? Cliente { get; set; }
    public Reserva? Reserva { get; set; }
    public InscripcionCompetencia? InscripcionCompetencia { get; set; }
    public InscripcionActividad? InscripcionActividad { get; set; }
    public TurnoParticular? TurnoParticular { get; set; }
    public Recibo? Recibo { get; set; }
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public ICollection<Reembolso> Reembolsos { get; set; } = new List<Reembolso>();
}

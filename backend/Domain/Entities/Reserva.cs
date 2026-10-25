namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Reserva de cancha vinculada a <see cref="Cancha"/>, <see cref="Cliente"/> y <see cref="PoliticaReserva"/> (RF020-RF025, RD01-RD06).
/// Mapeada a <c>public.reservas</c> en Supabase PostgreSQL.
/// </summary>
public class Reserva
{
    public string Id { get; set; } = string.Empty;
    public string CanchaId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string PoliticaReservaId { get; set; } = "pol-estandar";
    public string TitularDni { get; set; } = string.Empty;
    public string NombreCancha { get; set; } = string.Empty;
    public string Superficie { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public decimal DuracionHoras { get; set; }
    public string ModalidadJuego { get; set; } = "singles"; // 'singles' | 'dobles'
    public decimal CostoCancha { get; set; }
    public decimal CostoEquipamiento { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal SenaPagada { get; set; }
    public string? ReciboSenaId { get; set; }
    public string? MetodoPagoSena { get; set; }
    public decimal SaldoPendiente { get; set; }
    public bool SaldoAbonado { get; set; }
    public string? ReciboFinalId { get; set; }
    public string Estado { get; set; } = "pendiente_pago"; // 'pendiente_pago' | 'confirmada' | 'finalizada' | 'cancelada'
    public DateTimeOffset? FechaCancelacion { get; set; }
    public decimal? HorasAntelacionCancelacion { get; set; }
    public string? MotivoOPoliticaCancelacion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cancha? Cancha { get; set; }
    public Cliente? Cliente { get; set; }
    public PoliticaReserva? PoliticaReserva { get; set; }
    public ICollection<AsignacionEquipamiento> AsignacionesEquipamiento { get; set; } = new List<AsignacionEquipamiento>();
    public ICollection<Participante> Participantes { get; set; } = new List<Participante>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Recibo> Recibos { get; set; } = new List<Recibo>();
    public ICollection<Reembolso> Reembolsos { get; set; } = new List<Reembolso>();
}

namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Subclase TPT que hereda de <see cref="Usuario"/> y representa al socio/cliente del club.
/// Mapeada a <c>public.clientes</c> en Supabase PostgreSQL.
/// </summary>
public class Cliente : Usuario
{
    public string NumeroSocio { get; set; } = string.Empty;
    public DateOnly FechaAltaSocio { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int TotalReservas { get; set; }
    public decimal SaldoCreditoFavor { get; set; }
    public string? CategoriaJuego { get; set; }

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    public ICollection<Participante> ParticipacionesReserva { get; set; } = new List<Participante>();
    public ICollection<InscripcionCompetencia> InscripcionesCompetencia { get; set; } = new List<InscripcionCompetencia>();
    public ICollection<InscripcionActividad> InscripcionesActividad { get; set; } = new List<InscripcionActividad>();
    public ICollection<TurnoParticular> TurnosParticulares { get; set; } = new List<TurnoParticular>();
    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Recibo> Recibos { get; set; } = new List<Recibo>();
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public ICollection<Reembolso> Reembolsos { get; set; } = new List<Reembolso>();
    public ICollection<ClienteDescuento> ClienteDescuentos { get; set; } = new List<ClienteDescuento>();
    public ICollection<Descuento> Descuentos { get; set; } = new List<Descuento>();
}

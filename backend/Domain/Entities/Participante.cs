namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 3 — Jugador participante registrado en una <see cref="Reserva"/>.
/// Mapeado a <c>public.participantes</c> en Supabase PostgreSQL.
/// </summary>
public class Participante
{
    public string Id { get; set; } = string.Empty;
    public string ReservaId { get; set; } = string.Empty;
    public string? ClienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool EsTitular { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Reserva? Reserva { get; set; }
    public Cliente? Cliente { get; set; }
}

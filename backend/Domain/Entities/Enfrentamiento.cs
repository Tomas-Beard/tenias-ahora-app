namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Enfrentamiento o partido de un cuadro de torneo o jornada de liga (RF057-RF060).
/// Mapeado a <c>public.enfrentamientos</c> en Supabase PostgreSQL.
/// </summary>
public class Enfrentamiento
{
    public string Id { get; set; } = string.Empty;
    public string CompetenciaId { get; set; } = string.Empty;
    public string? CanchaId { get; set; }
    public string? EquipoLocalId { get; set; }
    public string? EquipoVisitanteId { get; set; }
    public string? EquipoGanadorId { get; set; }
    public string RondaOJornada { get; set; } = string.Empty;
    public string Jugador1Nombre { get; set; } = string.Empty;
    public string Jugador2Nombre { get; set; } = string.Empty;
    public string? GanadorNombre { get; set; }
    public string? NombreCancha { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public string MarcadorSets { get; set; } = "Pendiente";
    public string Estado { get; set; } = "programado"; // 'programado' | 'en_juego' | 'finalizado' | 'suspendido' | 'walkover'
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Competencia? Competencia { get; set; }
    public Cancha? Cancha { get; set; }
    public Equipo? EquipoLocal { get; set; }
    public Equipo? EquipoVisitante { get; set; }
    public Equipo? EquipoGanador { get; set; }
}

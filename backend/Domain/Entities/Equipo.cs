namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 4 — Equipo (pareja o jugador individual) y estadísticas en la tabla de posiciones de una <see cref="Competencia"/>.
/// Mapeado a <c>public.equipos</c> en Supabase PostgreSQL.
/// </summary>
public class Equipo
{
    public string Id { get; set; } = string.Empty;
    public string CompetenciaId { get; set; } = string.Empty;
    public string? JugadorTitularId { get; set; }
    public string? JugadorParejaId { get; set; }
    public string NombreEquipoOJugador { get; set; } = string.Empty;
    public string DniTitular { get; set; } = string.Empty;
    public string? DniPareja { get; set; }
    public string? NombrePareja { get; set; }
    public int PosicionRanking { get; set; } = 1;
    public int PartidosJugados { get; set; }
    public int PartidosGanados { get; set; }
    public int PartidosPerdidos { get; set; }
    public int SetsAFavor { get; set; }
    public int SetsEnContra { get; set; }
    public int DiferenciaSets { get; set; }
    public int Puntos { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Competencia? Competencia { get; set; }
    public Cliente? JugadorTitular { get; set; }
    public Cliente? JugadorPareja { get; set; }
    public ICollection<InscripcionCompetencia> Inscripciones { get; set; } = new List<InscripcionCompetencia>();
}

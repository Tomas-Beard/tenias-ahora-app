using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenisAhora.Application.Interfaces;
using TenisAhora.Domain.Entities;
using TenisAhora.Domain.Exceptions;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Controlador REST para el Módulo 4 (IEEE SRS-830): Competencias (<see cref="Competencia"/> — Torneos y Ligas),
/// Tipos de Competencia (<see cref="TipoCompetencia"/>), Reglamentos Internos (<see cref="Reglamento"/>),
/// Equipos y Tabla de Posiciones (<see cref="Equipo"/>), Inscripciones (<see cref="InscripcionCompetencia"/>)
/// y Enfrentamientos (<see cref="Enfrentamiento"/>).
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class CompetenciasController : ControllerBase
{
    private readonly TenisAhoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public CompetenciasController(
        TenisAhoraDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    // =========================================================================
    // 1. TIPOS DE COMPETENCIA (/api/tipos-competencia)
    // =========================================================================

    [HttpGet("tipos-competencia")]
    public async Task<IActionResult> GetTiposCompetencia(CancellationToken cancellationToken)
    {
        var list = await _dbContext.TiposCompetencia
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapTipoCompetenciaDto).ToList());
    }

    [HttpGet("tipos-competencia/{id}")]
    public async Task<IActionResult> GetTipoCompetenciaById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var tipo = await _dbContext.TiposCompetencia
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-competencia` el registro con id `{id}`.");
        }

        return Ok(MapTipoCompetenciaDto(tipo));
    }

    [HttpPost("tipos-competencia")]
    public async Task<IActionResult> CreateTipoCompetencia(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre del tipo de competencia es obligatorio.");
        }

        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"cmp-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var minP = GetInt(body, "minParticipants", 2);
        var maxP = GetInt(body, "maxParticipants", 32);

        var tipo = new TipoCompetencia
        {
            Id = id,
            Nombre = name,
            Formato = GetString(body, "format", "Cuadro eliminatorio"),
            AplicaA = GetString(body, "appliesTo", "Torneo"),
            SistemaPuntuacion = GetString(body, "scoringSystem"),
            MinParticipantes = minP <= 0 ? 2 : minP,
            MaxParticipantes = maxP <= 0 ? 32 : maxP,
            Descripcion = GetString(body, "description"),
            Activo = GetBool(body, "active", true),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.TiposCompetencia.Add(tipo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapTipoCompetenciaDto(tipo));
    }

    [HttpPut("tipos-competencia/{id}")]
    public async Task<IActionResult> UpdateTipoCompetencia(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var tipo = await _dbContext.TiposCompetencia
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-competencia` el registro con id `{id}`.");
        }

        if (body.TryGetProperty("name", out _))
        {
            var nextName = GetString(body, "name", tipo.Nombre).Trim();
            if (!string.IsNullOrWhiteSpace(nextName))
            {
                tipo.Nombre = nextName;
            }
        }
        if (body.TryGetProperty("format", out _))
        {
            tipo.Formato = GetString(body, "format", tipo.Formato);
        }
        if (body.TryGetProperty("appliesTo", out _))
        {
            tipo.AplicaA = GetString(body, "appliesTo", tipo.AplicaA);
        }
        if (body.TryGetProperty("scoringSystem", out _))
        {
            tipo.SistemaPuntuacion = GetString(body, "scoringSystem");
        }
        if (body.TryGetProperty("minParticipants", out _))
        {
            tipo.MinParticipantes = Math.Max(GetInt(body, "minParticipants", tipo.MinParticipantes), 2);
        }
        if (body.TryGetProperty("maxParticipants", out _))
        {
            tipo.MaxParticipantes = Math.Max(GetInt(body, "maxParticipants", tipo.MaxParticipantes), 2);
        }
        if (body.TryGetProperty("description", out _))
        {
            tipo.Descripcion = GetString(body, "description");
        }
        if (body.TryGetProperty("active", out _))
        {
            tipo.Activo = GetBool(body, "active", tipo.Activo);
        }

        tipo.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapTipoCompetenciaDto(tipo));
    }

    [HttpDelete("tipos-competencia/{id}")]
    public async Task<IActionResult> DeleteTipoCompetencia(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var tipo = await _dbContext.TiposCompetencia
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tipo is null)
        {
            throw DomainException.NotFound($"No se encontró en `tipos-competencia` el registro con id `{id}`.");
        }

        _dbContext.TiposCompetencia.Remove(tipo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    // =========================================================================
    // 2. TORNEOS (/api/torneos) Y LIGAS (/api/ligas) — CRUD
    // =========================================================================

    [HttpGet("torneos")]
    public async Task<IActionResult> GetTorneos(CancellationToken cancellationToken)
    {
        var list = await QueryCompetenciasByClassAsync("Torneo", cancellationToken);
        return Ok(list.Select(c => MapCompetenciaDto(c, isLiga: false)).ToList());
    }

    [HttpGet("torneos/{id}")]
    public async Task<IActionResult> GetTorneoById([FromRoute] string id, CancellationToken cancellationToken)
    {
        var comp = await RequireCompetenciaAsync(id, "torneos", cancellationToken);
        return Ok(MapCompetenciaDto(comp, isLiga: false));
    }

    [HttpPost("torneos")]
    public async Task<IActionResult> CreateTorneo([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var created = await CreateCompetenciaInternalAsync(body, isLiga: false, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, MapCompetenciaDto(created, isLiga: false));
    }

    [HttpPut("torneos/{id}")]
    public async Task<IActionResult> UpdateTorneo([FromRoute] string id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var updated = await UpdateCompetenciaInternalAsync(id, "torneos", body, cancellationToken);
        return Ok(MapCompetenciaDto(updated, isLiga: false));
    }

    [HttpDelete("torneos/{id}")]
    public async Task<IActionResult> DeleteTorneo([FromRoute] string id, CancellationToken cancellationToken)
    {
        await DeleteCompetenciaInternalAsync(id, "torneos", cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet("ligas")]
    public async Task<IActionResult> GetLigas(CancellationToken cancellationToken)
    {
        var list = await QueryCompetenciasByClassAsync("Liga", cancellationToken);
        return Ok(list.Select(c => MapCompetenciaDto(c, isLiga: true)).ToList());
    }

    [HttpGet("ligas/{id}")]
    public async Task<IActionResult> GetLigaById([FromRoute] string id, CancellationToken cancellationToken)
    {
        var comp = await RequireCompetenciaAsync(id, "ligas", cancellationToken);
        return Ok(MapCompetenciaDto(comp, isLiga: true));
    }

    [HttpPost("ligas")]
    public async Task<IActionResult> CreateLiga([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var created = await CreateCompetenciaInternalAsync(body, isLiga: true, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, MapCompetenciaDto(created, isLiga: true));
    }

    [HttpPut("ligas/{id}")]
    public async Task<IActionResult> UpdateLiga([FromRoute] string id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var updated = await UpdateCompetenciaInternalAsync(id, "ligas", body, cancellationToken);
        return Ok(MapCompetenciaDto(updated, isLiga: true));
    }

    [HttpDelete("ligas/{id}")]
    public async Task<IActionResult> DeleteLiga([FromRoute] string id, CancellationToken cancellationToken)
    {
        await DeleteCompetenciaInternalAsync(id, "ligas", cancellationToken);
        return Ok(new { success = true });
    }

    // =========================================================================
    // 3. REGLAMENTO INTERNO (/api/torneos/{id}/reglamento, /api/ligas/{id}/reglamento)
    // =========================================================================

    [HttpPut("torneos/{id}/reglamento")]
    public async Task<IActionResult> UpdateTorneoReglamento(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var updated = await UpdateReglamentoInternalAsync(id, "torneos", body, cancellationToken);
        return Ok(MapCompetenciaDto(updated, isLiga: false));
    }

    [HttpPut("ligas/{id}/reglamento")]
    public async Task<IActionResult> UpdateLigaReglamento(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var updated = await UpdateReglamentoInternalAsync(id, "ligas", body, cancellationToken);
        return Ok(MapCompetenciaDto(updated, isLiga: true));
    }

    // =========================================================================
    // 4. INSCRIPCIONES (/api/torneos/{id}/inscripciones, /api/ligas/{id}/inscripciones)
    // =========================================================================

    [HttpPost("torneos/{id}/inscripciones")]
    public async Task<IActionResult> EnrollTorneo(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var result = await EnrollParticipantInternalAsync(id, "torneos", isLiga: false, body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("torneos/{id}/inscripciones/{participantId}")]
    public async Task<IActionResult> UnenrollTorneo(
        [FromRoute] string id,
        [FromRoute] string participantId,
        CancellationToken cancellationToken)
    {
        await UnenrollParticipantInternalAsync(id, "torneos", participantId, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("ligas/{id}/inscripciones")]
    public async Task<IActionResult> EnrollLiga(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var result = await EnrollParticipantInternalAsync(id, "ligas", isLiga: true, body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("ligas/{id}/inscripciones/{participantId}")]
    public async Task<IActionResult> UnenrollLiga(
        [FromRoute] string id,
        [FromRoute] string participantId,
        CancellationToken cancellationToken)
    {
        await UnenrollParticipantInternalAsync(id, "ligas", participantId, cancellationToken);
        return Ok(new { success = true });
    }

    // =========================================================================
    // 5. ENFRENTAMIENTOS Y RESULTADOS (/enfrentamientos y /resultado)
    // =========================================================================

    [HttpPost("torneos/{id}/enfrentamientos")]
    public async Task<IActionResult> AddTorneoMatch(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var match = await AddMatchInternalAsync(id, "torneos", defaultRoundPrefix: "Ronda", matchIdPrefix: "tm", body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { success = true, match });
    }

    [HttpPut("torneos/{id}/enfrentamientos/{matchId}")]
    public async Task<IActionResult> UpdateTorneoMatch(
        [FromRoute] string id,
        [FromRoute] string matchId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var match = await UpdateMatchInternalAsync(id, "torneos", matchId, body, cancellationToken);
        return Ok(new { success = true, match });
    }

    [HttpPost("ligas/{id}/enfrentamientos")]
    public async Task<IActionResult> AddLigaMatch(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var match = await AddMatchInternalAsync(id, "ligas", defaultRoundPrefix: "Jornada", matchIdPrefix: "lm", body, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { success = true, match });
    }

    [HttpPut("ligas/{id}/enfrentamientos/{matchId}")]
    public async Task<IActionResult> UpdateLigaMatch(
        [FromRoute] string id,
        [FromRoute] string matchId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var match = await UpdateMatchInternalAsync(id, "ligas", matchId, body, cancellationToken);
        return Ok(new { success = true, match });
    }

    /// <summary>
    /// POST /api/ligas/{id}/enfrentamientos/{matchId}/resultado — Registra el marcador y ganador del partido,
    /// marca el enfrentamiento como finalizado y recalcula la tabla de posiciones (<see cref="Equipo"/>: PJ, PG, PP, SF, SC, DIF, Puntos).
    /// </summary>
    [HttpPost("ligas/{id}/enfrentamientos/{matchId}/resultado")]
    public async Task<IActionResult> RecordLigaMatchResult(
        [FromRoute] string id,
        [FromRoute] string matchId,
        [FromBody] RecordMatchScoreRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var winner = (request.Winner ?? string.Empty).Trim();
        var score = (request.Score ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(winner))
        {
            throw DomainException.BadRequest("Debe indicar el ganador.");
        }

        var comp = await _dbContext.Competencias
            .Include(c => c.Equipos)
            .Include(c => c.Enfrentamientos)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (comp is null)
        {
            throw DomainException.NotFound($"No se encontró en `ligas` el registro con id `{id}`.");
        }

        var enf = comp.Enfrentamientos.FirstOrDefault(e => e.Id == matchId);
        if (enf is null)
        {
            throw DomainException.NotFound($"No existe el enfrentamiento {matchId}.");
        }

        if (!string.Equals(winner, enf.Jugador1Nombre, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(winner, enf.Jugador2Nombre, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest("El ganador debe ser uno de los dos jugadores del partido.");
        }

        var alreadyFinished = string.Equals(enf.Estado, "finalizado", StringComparison.OrdinalIgnoreCase);

        enf.MarcadorSets = string.IsNullOrWhiteSpace(score) ? "6-4, 6-4" : score;
        enf.GanadorNombre = winner;
        enf.Estado = "finalizado";
        enf.UpdatedAt = DateTimeOffset.UtcNow;

        if (!alreadyFinished)
        {
            var loser = string.Equals(winner, enf.Jugador1Nombre, StringComparison.OrdinalIgnoreCase)
                ? enf.Jugador2Nombre
                : enf.Jugador1Nombre;

            var winnerTeam = comp.Equipos.FirstOrDefault(eq =>
                string.Equals(eq.NombreEquipoOJugador, winner, StringComparison.OrdinalIgnoreCase));
            var loserTeam = comp.Equipos.FirstOrDefault(eq =>
                string.Equals(eq.NombreEquipoOJugador, loser, StringComparison.OrdinalIgnoreCase));

            if (winnerTeam is not null)
            {
                winnerTeam.PartidosJugados += 1;
                winnerTeam.PartidosGanados += 1;
                winnerTeam.Puntos += 3;
                winnerTeam.SetsAFavor += 2;
                winnerTeam.DiferenciaSets += 2;
                winnerTeam.UpdatedAt = DateTimeOffset.UtcNow;
                enf.EquipoGanadorId = winnerTeam.Id;
            }

            if (loserTeam is not null)
            {
                loserTeam.PartidosJugados += 1;
                loserTeam.PartidosPerdidos += 1;
                loserTeam.SetsEnContra += 2;
                loserTeam.DiferenciaSets -= 2;
                loserTeam.UpdatedAt = DateTimeOffset.UtcNow;
            }

            RerankEquipos(comp.Equipos);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var standings = comp.Equipos
            .OrderBy(eq => eq.PosicionRanking)
            .Select(MapStandingDto)
            .ToList();

        return Ok(new
        {
            success = true,
            match = MapMatchDto(enf),
            standings
        });
    }

    // =========================================================================
    // MÉTODOS INTERNOS DE LÓGICA DE NEGOCIO (MÓDULO 4)
    // =========================================================================

    private void EnsureStaff()
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }
    }

    private async Task<List<Competencia>> QueryCompetenciasByClassAsync(
        string claseCompetencia,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Competencias
            .AsNoTracking()
            .Include(c => c.Reglamento)
            .Include(c => c.Inscripciones)
            .Include(c => c.Equipos)
            .Include(c => c.Enfrentamientos)
            .Where(c => c.ClaseCompetencia.ToLower() == claseCompetencia.ToLower())
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<Competencia> RequireCompetenciaAsync(
        string id,
        string collectionName,
        CancellationToken cancellationToken)
    {
        var comp = await _dbContext.Competencias
            .Include(c => c.Reglamento)
            .Include(c => c.Inscripciones)
            .Include(c => c.Equipos)
            .Include(c => c.Enfrentamientos)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (comp is null)
        {
            throw DomainException.NotFound($"No se encontró en `{collectionName}` el registro con id `{id}`.");
        }

        return comp;
    }

    private async Task<Competencia> CreateCompetenciaInternalAsync(
        JsonElement body,
        bool isLiga,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre de la competencia es obligatorio.");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var prefix = isLiga ? "liga" : "trn";
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"{prefix}-{nowMs}";
        }

        var defaultRules = isLiga
            ? "Reglamento Interno: Sistema todos contra todos por puntos."
            : "Reglamento Interno: Partidos al mejor de 3 sets con tie-break oficial.";

        var rulesText = GetString(body, "internalRules", defaultRules);
        var reglamento = new Reglamento
        {
            Id = $"reg-{id}",
            CodigoReglamento = $"REG-AAT-{nowMs % 100000:00000}",
            Titulo = $"Reglamento Oficial - {name}",
            NormativaBaseAsociacion = "Reglas Oficiales Asociación Argentina de Tenis (AAT)",
            TextoReglamentoInterno = string.IsNullOrWhiteSpace(rulesText) ? defaultRules : rulesText,
            FechaVigencia = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        await ApplyDocumentReferenceToReglamentoAsync(reglamento, body, cancellationToken);

        var defaultTipoId = isLiga ? "cmp-liga" : "cmp-elim";
        var tipoComp = await _dbContext.TiposCompetencia
            .FirstOrDefaultAsync(t => t.Id == defaultTipoId || t.AplicaA.ToLower() == (isLiga ? "liga" : "torneo"), cancellationToken);

        if (tipoComp is null)
        {
            tipoComp = new TipoCompetencia
            {
                Id = defaultTipoId,
                Nombre = isLiga ? "Liga todos contra todos" : "Torneo de eliminación directa",
                Formato = isLiga ? "Round robin por puntos" : "Cuadro eliminatorio",
                AplicaA = isLiga ? "Liga" : "Torneo",
                MinParticipantes = 4,
                MaxParticipantes = 16,
                Activo = true
            };
            _dbContext.TiposCompetencia.Add(tipoComp);
        }

        var startDate = ParseDateOrDefault(GetString(body, "startDate"), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        var endDate = ParseDateOrDefault(GetString(body, "endDate"), startDate.AddDays(14));
        if (endDate < startDate)
        {
            endDate = startDate;
        }

        var maxP = GetInt(body, "maxParticipants", isLiga ? 8 : 16);
        if (maxP < 2)
        {
            maxP = isLiga ? 8 : 16;
        }

        var comp = new Competencia
        {
            Id = id,
            TipoCompetenciaId = tipoComp.Id,
            ReglamentoId = reglamento.Id,
            Reglamento = reglamento,
            Nombre = name,
            ClaseCompetencia = isLiga ? "Liga" : "Torneo",
            Categoria = GetString(body, "category", "Libre"),
            Modalidad = NormalizeModality(GetString(body, "modality", "Singles")),
            SuperficieRequerida = GetString(body, "surfaceRequired", "Ladrillo"),
            FechaInicio = startDate,
            FechaFin = endDate,
            CostoInscripcion = Math.Max(GetDecimal(body, "registrationFee", 0m), 0m),
            MaxParticipantes = maxP,
            InscriptosActuales = 0,
            Estado = GetString(body, "status", "inscripcion_abierta"),
            Descripcion = GetString(body, "description"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Reglamentos.Add(reglamento);
        _dbContext.Competencias.Add(comp);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await RequireCompetenciaAsync(comp.Id, isLiga ? "ligas" : "torneos", cancellationToken);
    }

    private async Task<Competencia> UpdateCompetenciaInternalAsync(
        string id,
        string collectionName,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);

        if (body.TryGetProperty("name", out _))
        {
            var nextName = GetString(body, "name", comp.Nombre).Trim();
            if (!string.IsNullOrWhiteSpace(nextName))
            {
                comp.Nombre = nextName;
            }
        }
        if (body.TryGetProperty("category", out _))
        {
            comp.Categoria = GetString(body, "category", comp.Categoria);
        }
        if (body.TryGetProperty("modality", out _))
        {
            comp.Modalidad = NormalizeModality(GetString(body, "modality", comp.Modalidad));
        }
        if (body.TryGetProperty("surfaceRequired", out _))
        {
            comp.SuperficieRequerida = GetString(body, "surfaceRequired", comp.SuperficieRequerida ?? "Ladrillo");
        }
        if (body.TryGetProperty("startDate", out _))
        {
            comp.FechaInicio = ParseDateOrDefault(GetString(body, "startDate"), comp.FechaInicio);
        }
        if (body.TryGetProperty("endDate", out _))
        {
            var nextEnd = ParseDateOrDefault(GetString(body, "endDate"), comp.FechaFin);
            comp.FechaFin = nextEnd < comp.FechaInicio ? comp.FechaInicio : nextEnd;
        }
        if (body.TryGetProperty("registrationFee", out _))
        {
            comp.CostoInscripcion = Math.Max(GetDecimal(body, "registrationFee", comp.CostoInscripcion), 0m);
        }
        if (body.TryGetProperty("maxParticipants", out _))
        {
            var nextMax = GetInt(body, "maxParticipants", comp.MaxParticipantes);
            comp.MaxParticipantes = Math.Max(nextMax, Math.Max(comp.InscriptosActuales, 2));
        }
        if (body.TryGetProperty("status", out _))
        {
            comp.Estado = GetString(body, "status", comp.Estado);
        }
        if (body.TryGetProperty("description", out _))
        {
            comp.Descripcion = GetString(body, "description");
        }

        if (comp.Reglamento is not null)
        {
            if (body.TryGetProperty("internalRules", out _))
            {
                var rules = GetString(body, "internalRules", comp.Reglamento.TextoReglamentoInterno);
                if (!string.IsNullOrWhiteSpace(rules))
                {
                    comp.Reglamento.TextoReglamentoInterno = rules;
                }
            }
            await ApplyDocumentReferenceToReglamentoAsync(comp.Reglamento, body, cancellationToken);
            comp.Reglamento.UpdatedAt = DateTimeOffset.UtcNow;
        }

        comp.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return comp;
    }

    private async Task DeleteCompetenciaInternalAsync(
        string id,
        string collectionName,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);
        _dbContext.Competencias.Remove(comp);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Competencia> UpdateReglamentoInternalAsync(
        string id,
        string collectionName,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);
        if (comp.Reglamento is null)
        {
            comp.Reglamento = new Reglamento
            {
                Id = $"reg-{comp.Id}",
                CodigoReglamento = $"REG-{comp.Id.ToUpperInvariant()}",
                Titulo = $"Reglamento Oficial - {comp.Nombre}",
                NormativaBaseAsociacion = "Reglas Oficiales Asociación Argentina de Tenis (AAT)",
                TextoReglamentoInterno = "Reglamento Interno Oficial."
            };
            comp.ReglamentoId = comp.Reglamento.Id;
            _dbContext.Reglamentos.Add(comp.Reglamento);
        }

        if (body.TryGetProperty("internalRules", out _))
        {
            var nextRules = GetString(body, "internalRules");
            if (!string.IsNullOrWhiteSpace(nextRules))
            {
                comp.Reglamento.TextoReglamentoInterno = nextRules;
            }
        }

        await ApplyDocumentReferenceToReglamentoAsync(comp.Reglamento, body, cancellationToken);
        comp.Reglamento.UpdatedAt = DateTimeOffset.UtcNow;
        comp.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return comp;
    }

    private async Task ApplyDocumentReferenceToReglamentoAsync(
        Reglamento reglamento,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!body.TryGetProperty("internalRulesDocument", out var docProp))
        {
            return;
        }

        if (docProp.ValueKind == JsonValueKind.Null)
        {
            reglamento.DocumentoNombre = null;
            reglamento.DocumentoMimeType = null;
            reglamento.DocumentoTamanoBytes = null;
            reglamento.DocumentoBase64OUrl = null;
            return;
        }

        if (docProp.ValueKind == JsonValueKind.Object)
        {
            var docId = GetString(docProp, "id");
            var docName = GetString(docProp, "name");
            var docMime = GetString(docProp, "mimeType", "application/pdf");
            var docSize = GetInt(docProp, "size", 0);

            if (!string.IsNullOrWhiteSpace(docId))
            {
                var resolved = await AcademiaDocumentosController.TryResolveDocumentAsync(_dbContext, docId, cancellationToken);
                if (resolved is not null)
                {
                    reglamento.DocumentoNombre = resolved.Value.Name;
                    reglamento.DocumentoMimeType = resolved.Value.MimeType;
                    reglamento.DocumentoTamanoBytes = resolved.Value.Size;
                    reglamento.DocumentoBase64OUrl = resolved.Value.Base64Data;
                    return;
                }
            }

            reglamento.DocumentoNombre = !string.IsNullOrWhiteSpace(docName) ? docName : "Reglamento_Interno.pdf";
            reglamento.DocumentoMimeType = docMime;
            reglamento.DocumentoTamanoBytes = docSize > 0 ? docSize : 1024;
        }
    }

    private async Task<object> EnrollParticipantInternalAsync(
        string id,
        string collectionName,
        bool isLiga,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);

        var dni = GetString(body, "dni", _currentUser.Dni).Trim();
        if (string.IsNullOrWhiteSpace(dni))
        {
            dni = "00.000.000";
        }

        if (!_currentUser.IsStaff)
        {
            if (!_currentUser.IsMember)
            {
                throw DomainException.Forbidden("Esta acción es solo para el personal del club o socios.");
            }
            if (!_currentUser.IsMeByDni(dni))
            {
                throw DomainException.Forbidden("Solo podés inscribirte a vos mismo.");
            }
        }

        var activeInscriptions = comp.Inscripciones.Where(i => i.Estado != "baja").ToList();
        if (comp.MaxParticipantes > 0 && activeInscriptions.Count >= comp.MaxParticipantes)
        {
            throw DomainException.Conflict(
                isLiga
                    ? "La liga alcanzó el cupo máximo de inscriptos."
                    : "El torneo alcanzó el cupo máximo de inscriptos.");
        }

        var duplicateDni = activeInscriptions.Any(i =>
            string.Equals(NormalizeDni(i.DniParticipante), NormalizeDni(dni), StringComparison.OrdinalIgnoreCase));
        if (duplicateDni)
        {
            throw DomainException.Conflict($"Ya existe un participante inscripto con el DNI {dni} en esta competencia.");
        }

        var name = GetString(body, "name", GetString(body, "partnerName", "Participante Inscripto")).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Participante Inscripto";
        }

        var phone = GetString(body, "phone");
        var partnerName = GetString(body, "partnerName");
        var partnerDni = GetString(body, "partnerDni");

        var cliente = await ResolveOrCreateClienteByDniAsync(dni, name, phone, cancellationToken);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Equipo? equipo = comp.Equipos.FirstOrDefault(eq =>
            string.Equals(eq.NombreEquipoOJugador, name, StringComparison.OrdinalIgnoreCase));

        if (equipo is null)
        {
            equipo = new Equipo
            {
                Id = $"eq-{comp.Id}-{nowMs}",
                CompetenciaId = comp.Id,
                JugadorTitularId = cliente.Id,
                NombreEquipoOJugador = name,
                DniTitular = dni,
                NombrePareja = string.IsNullOrWhiteSpace(partnerName) ? null : partnerName,
                DniPareja = string.IsNullOrWhiteSpace(partnerDni) ? null : partnerDni,
                PosicionRanking = comp.Equipos.Count + 1,
                PartidosJugados = 0,
                PartidosGanados = 0,
                PartidosPerdidos = 0,
                SetsAFavor = 0,
                SetsEnContra = 0,
                DiferenciaSets = 0,
                Puntos = 0
            };
            _dbContext.Equipos.Add(equipo);
        }

        var prefix = isLiga ? "lp" : "p";
        var insc = new InscripcionCompetencia
        {
            Id = $"{prefix}-{nowMs}",
            CompetenciaId = comp.Id,
            ClienteId = cliente.Id,
            EquipoId = equipo.Id,
            NombreParticipante = name,
            DniParticipante = dni,
            TelefonoParticipante = phone,
            NombrePareja = string.IsNullOrWhiteSpace(partnerName) ? null : partnerName,
            DniPareja = string.IsNullOrWhiteSpace(partnerDni) ? null : partnerDni,
            FechaInscripcion = DateOnly.FromDateTime(DateTime.UtcNow),
            MontoInscripcion = comp.CostoInscripcion,
            Estado = "confirmada"
        };

        _dbContext.InscripcionesCompetencia.Add(insc);
        comp.InscriptosActuales = activeInscriptions.Count + 1;
        comp.UpdatedAt = DateTimeOffset.UtcNow;

        // Generar Pago y Recibo oficial de inscripción (respetando CHECK monto > 0 en public.pagos)
        var receiptId = $"REC-INSC-{Random.Shared.Next(1000, 9999)}";
        var effectivePaymentAmount = comp.CostoInscripcion > 0 ? comp.CostoInscripcion : 1m;
        var pago = new Pago
        {
            Id = $"PAG-{receiptId}",
            ClienteId = cliente.Id,
            InscripcionCompetenciaId = insc.Id,
            TipoPago = "inscripcion",
            MedioPago = "Mercado Pago (QR)",
            Monto = effectivePaymentAmount,
            Estado = "completado",
            QrData = $"https://mpago.la/pos/tenisahora/{receiptId.ToLowerInvariant()}",
            FechaPago = DateTimeOffset.UtcNow
        };

        var itemsJson = JsonSerializer.Serialize(new[]
        {
            new { description = $"Inscripción a {(isLiga ? "liga" : "torneo")} {comp.Nombre}", amount = comp.CostoInscripcion }
        });

        var recibo = new Recibo
        {
            Id = receiptId,
            NumeroRecibo = receiptId,
            PagoId = pago.Id,
            ClienteId = cliente.Id,
            ClienteNombre = name,
            ClienteDni = dni,
            Concepto = $"Inscripción {comp.Nombre}",
            ItemsJson = itemsJson,
            TotalAbonado = comp.CostoInscripcion,
            MedioPago = "Mercado Pago (QR)",
            Estado = "completado",
            QrData = pago.QrData,
            CajeroNombre = _currentUser.IsStaff ? "Recepción del Club" : "Portal del socio",
            FechaEmision = DateTimeOffset.UtcNow
        };

        _dbContext.Pagos.Add(pago);
        _dbContext.Recibos.Add(recibo);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new
        {
            success = true,
            participant = MapParticipantDto(insc),
            receipt = new
            {
                id = recibo.Id,
                clientName = recibo.ClienteNombre,
                clientDni = recibo.ClienteDni,
                concept = recibo.Concepto,
                totalPaid = recibo.TotalAbonado,
                paymentMethod = recibo.MedioPago,
                status = recibo.Estado,
                date = recibo.FechaEmision.ToString("yyyy-MM-dd HH:mm"),
                qrData = recibo.QrData
            }
        };
    }

    private async Task UnenrollParticipantInternalAsync(
        string id,
        string collectionName,
        string participantId,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);
        var insc = comp.Inscripciones.FirstOrDefault(i => i.Id == participantId);

        if (insc is not null)
        {
            var removedName = insc.NombreParticipante;
            _dbContext.InscripcionesCompetencia.Remove(insc);

            var eq = comp.Equipos.FirstOrDefault(e =>
                e.Id == insc.EquipoId ||
                string.Equals(e.NombreEquipoOJugador, removedName, StringComparison.OrdinalIgnoreCase));

            if (eq is not null)
            {
                comp.Equipos.Remove(eq);
                _dbContext.Equipos.Remove(eq);
            }

            RerankEquipos(comp.Equipos);
            comp.InscriptosActuales = Math.Max(comp.Inscripciones.Count(i => i.Id != participantId && i.Estado != "baja"), 0);
            comp.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<object> AddMatchInternalAsync(
        string id,
        string collectionName,
        string defaultRoundPrefix,
        string matchIdPrefix,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var p1 = GetString(body, "p1").Trim();
        var p2 = GetString(body, "p2").Trim();
        if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2))
        {
            throw DomainException.BadRequest("p1 y p2 son obligatorios.");
        }
        if (string.Equals(p1, p2, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest("Los dos jugadores deben ser distintos.");
        }

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);

        var distinctRounds = comp.Enfrentamientos.Select(e => e.RondaOJornada).Distinct().Count();
        var roundName = GetString(body, "round").Trim();
        if (string.IsNullOrWhiteSpace(roundName))
        {
            roundName = $"{defaultRoundPrefix} {distinctRounds + 1}";
        }

        var courtName = GetString(body, "court", "Cancha 1").Trim();
        var dateRaw = GetString(body, "date", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        var (parsedDate, parsedTime) = ParseMatchDateAndTime(dateRaw);

        var enf = new Enfrentamiento
        {
            Id = $"{matchIdPrefix}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            CompetenciaId = comp.Id,
            RondaOJornada = roundName,
            Jugador1Nombre = p1,
            Jugador2Nombre = p2,
            GanadorNombre = null,
            NombreCancha = courtName,
            Fecha = parsedDate,
            HoraInicio = parsedTime,
            MarcadorSets = "Pendiente",
            Estado = "programado",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Enfrentamientos.Add(enf);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMatchDto(enf);
    }

    private async Task<object> UpdateMatchInternalAsync(
        string id,
        string collectionName,
        string matchId,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var comp = await RequireCompetenciaAsync(id, collectionName, cancellationToken);
        var enf = comp.Enfrentamientos.FirstOrDefault(e => e.Id == matchId);
        if (enf is null)
        {
            throw DomainException.NotFound($"No existe el enfrentamiento {matchId}.");
        }

        if (body.TryGetProperty("p1", out _))
        {
            var nextP1 = GetString(body, "p1", enf.Jugador1Nombre).Trim();
            if (!string.IsNullOrWhiteSpace(nextP1))
            {
                enf.Jugador1Nombre = nextP1;
            }
        }
        if (body.TryGetProperty("p2", out _))
        {
            var nextP2 = GetString(body, "p2", enf.Jugador2Nombre).Trim();
            if (!string.IsNullOrWhiteSpace(nextP2))
            {
                enf.Jugador2Nombre = nextP2;
            }
        }
        if (body.TryGetProperty("score", out _))
        {
            enf.MarcadorSets = GetString(body, "score", enf.MarcadorSets);
        }
        if (body.TryGetProperty("winner", out var wProp))
        {
            enf.GanadorNombre = wProp.ValueKind == JsonValueKind.Null ? null : GetString(body, "winner");
        }
        if (body.TryGetProperty("status", out _))
        {
            enf.Estado = GetString(body, "status", enf.Estado);
        }
        if (body.TryGetProperty("court", out _))
        {
            enf.NombreCancha = GetString(body, "court", enf.NombreCancha ?? "Cancha 1");
        }
        if (body.TryGetProperty("date", out _))
        {
            var (d, t) = ParseMatchDateAndTime(GetString(body, "date"));
            enf.Fecha = d;
            enf.HoraInicio = t;
        }

        enf.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMatchDto(enf);
    }

    private async Task<Cliente> ResolveOrCreateClienteByDniAsync(
        string dni,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Clientes
            .FirstOrDefaultAsync(c => c.Dni == dni, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var existingUsuario = await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Dni == dni, cancellationToken);

        if (existingUsuario is not null)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO public.clientes (id, numero_socio, fecha_alta_socio, total_reservas, saldo_credito_favor) VALUES ({existingUsuario.Id}, {$"TA-{existingUsuario.Id}"}, CURRENT_DATE, 0, 0) ON CONFLICT (id) DO NOTHING",
                cancellationToken);
            return await _dbContext.Clientes.FirstAsync(c => c.Id == existingUsuario.Id, cancellationToken);
        }

        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var nombre = parts.Length > 0 ? parts[0] : "Socio";
        var apellido = parts.Length > 1 ? parts[1] : "Tenis Ahora";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nuevo = new Cliente
        {
            Id = $"usr-{nowMs}",
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = fullName,
            Dni = dni,
            Telefono = phone,
            Email = $"socio.{nowMs}@tenisahora.com",
            Rol = "client",
            Activo = true,
            NumeroSocio = $"TA-{1000 + (nowMs % 9000)}",
            FechaAltaSocio = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _dbContext.Clientes.Add(nuevo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return nuevo;
    }

    private static void RerankEquipos(ICollection<Equipo> equipos)
    {
        var sorted = equipos
            .OrderByDescending(e => e.Puntos)
            .ThenByDescending(e => e.DiferenciaSets)
            .ThenBy(e => e.NombreEquipoOJugador)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].PosicionRanking = i + 1;
        }
    }

    private static object MapTipoCompetenciaDto(TipoCompetencia t) => new
    {
        id = t.Id,
        name = t.Nombre,
        format = t.Formato,
        appliesTo = t.AplicaA,
        scoringSystem = t.SistemaPuntuacion ?? string.Empty,
        minParticipants = t.MinParticipantes,
        maxParticipants = t.MaxParticipantes,
        description = t.Descripcion ?? string.Empty,
        active = t.Activo
    };

    private static Dictionary<string, object?> MapCompetenciaDto(Competencia c, bool isLiga)
    {
        var participants = c.Inscripciones
            .Where(i => i.Estado != "baja")
            .OrderBy(i => i.CreatedAt)
            .Select(MapParticipantDto)
            .ToList();

        var rounds = c.Enfrentamientos
            .OrderBy(e => e.CreatedAt)
            .GroupBy(e => e.RondaOJornada)
            .Select(g => new
            {
                round = g.Key,
                matches = g.Select(MapMatchDto).ToList()
            })
            .ToList();

        object? rulesDoc = null;
        if (c.Reglamento is not null && !string.IsNullOrWhiteSpace(c.Reglamento.DocumentoNombre))
        {
            rulesDoc = new
            {
                id = $"doc-reg-{c.Reglamento.Id}",
                name = c.Reglamento.DocumentoNombre,
                mimeType = c.Reglamento.DocumentoMimeType ?? "application/pdf",
                size = c.Reglamento.DocumentoTamanoBytes ?? 1024
            };
        }

        var dict = new Dictionary<string, object?>
        {
            ["id"] = c.Id,
            ["name"] = c.Nombre,
            ["category"] = c.Categoria,
            ["modality"] = c.Modalidad == "Dobles" ? "Pareja" : c.Modalidad,
            ["surfaceRequired"] = c.SuperficieRequerida ?? "Ladrillo",
            ["startDate"] = c.FechaInicio.ToString("yyyy-MM-dd"),
            ["endDate"] = c.FechaFin.ToString("yyyy-MM-dd"),
            ["registrationFee"] = c.CostoInscripcion,
            ["maxParticipants"] = c.MaxParticipantes,
            ["currentEnrolled"] = participants.Count > 0 ? participants.Count : c.InscriptosActuales,
            ["status"] = c.Estado,
            ["description"] = c.Descripcion ?? string.Empty,
            ["internalRules"] = c.Reglamento?.TextoReglamentoInterno ?? string.Empty,
            ["participants"] = participants
        };

        if (rulesDoc is not null)
        {
            dict["internalRulesDocument"] = rulesDoc;
        }

        if (isLiga)
        {
            dict["standings"] = c.Equipos
                .OrderBy(eq => eq.PosicionRanking)
                .Select(MapStandingDto)
                .ToList();
            dict["fixtures"] = rounds;
        }
        else
        {
            dict["bracket"] = rounds;
        }

        return dict;
    }

    private static object MapParticipantDto(InscripcionCompetencia i) => new
    {
        id = i.Id,
        name = i.NombreParticipante,
        dni = i.DniParticipante,
        phone = i.TelefonoParticipante ?? string.Empty,
        partnerName = i.NombrePareja,
        partnerDni = i.DniPareja,
        enrolledAt = i.FechaInscripcion.ToString("yyyy-MM-dd")
    };

    private static object MapStandingDto(Equipo eq) => new
    {
        rank = eq.PosicionRanking,
        name = eq.NombreEquipoOJugador,
        pj = eq.PartidosJugados,
        pg = eq.PartidosGanados,
        pp = eq.PartidosPerdidos,
        sf = eq.SetsAFavor,
        sc = eq.SetsEnContra,
        dif = eq.DiferenciaSets,
        points = eq.Puntos
    };

    private static object MapMatchDto(Enfrentamiento e)
    {
        var dateStr = e.HoraInicio.HasValue
            ? $"{e.Fecha:yyyy-MM-dd} {e.HoraInicio.Value:HH\\:mm}"
            : e.Fecha.ToString("yyyy-MM-dd");

        return new
        {
            id = e.Id,
            p1 = e.Jugador1Nombre,
            p2 = e.Jugador2Nombre,
            score = e.MarcadorSets == "Pendiente" ? string.Empty : e.MarcadorSets,
            winner = e.GanadorNombre,
            status = e.Estado,
            court = e.NombreCancha ?? "Cancha 1",
            date = dateStr
        };
    }

    private static string NormalizeModality(string raw)
    {
        return string.Equals(raw?.Trim(), "Pareja", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw?.Trim(), "Dobles", StringComparison.OrdinalIgnoreCase)
            ? "Dobles"
            : "Singles";
    }

    private static (DateOnly Date, TimeOnly? Time) ParseMatchDateAndTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (DateOnly.FromDateTime(DateTime.UtcNow), null);
        }

        var parts = raw.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var d = DateOnly.TryParse(parts[0], out var pd) ? pd : DateOnly.FromDateTime(DateTime.UtcNow);
        TimeOnly? t = parts.Length > 1 && TimeOnly.TryParse(parts[1], out var pt) ? pt : null;
        return (d, t);
    }

    private static string NormalizeDni(string raw) =>
        raw.Trim().Replace(".", string.Empty).Replace("-", string.Empty);

    private static DateOnly ParseDateOrDefault(string? raw, DateOnly fallback) =>
        DateOnly.TryParse(raw, out var d) ? d : fallback;

    private static string GetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? defaultValue,
                JsonValueKind.Number => prop.GetRawText(),
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private static int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
            {
                return val;
            }
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var decVal))
            {
                return (int)Math.Round(decVal);
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        return defaultValue;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName, decimal defaultValue = 0m)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val))
            {
                return val;
            }
            if (prop.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return defaultValue;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }
}

public sealed class RecordMatchScoreRequestDto
{
    public string? Score { get; set; }
    public string? Winner { get; set; }
}

using System.Collections.Concurrent;
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
/// Controlador REST para el Módulo 5 (IEEE SRS-830): Profesores y Entrenadores (<see cref="Profesional"/> TPT),
/// Certificados Habilitantes AAT (<see cref="Certificado"/> — RD08, RD09), Disponibilidad (<see cref="HorarioProfesional"/>),
/// Actividades Grupales (<see cref="ActividadGrupal"/> / <see cref="Clase"/> / <see cref="Entrenamiento"/> TPT — RD07, RD10),
/// Inscripciones (<see cref="InscripcionActividad"/>), Asistencia (<see cref="Asistencia"/>),
/// Turnos Particulares (<see cref="TurnoParticular"/>) y Gestión de Documentos Digitales.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class AcademiaDocumentosController : ControllerBase
{
    private const int MaxDocumentBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] DefaultSessions =
    [
        "02/10", "04/10", "09/10", "11/10", "16/10", "18/10", "23/10", "25/10"
    ];

    // Almacén en memoria para documentos recién subidos antes de ser asociados a un Certificado o Reglamento,
    // pre-poblado con los 3 PDFs semilla oficiales de la AAT (seed.json).
    private static readonly ConcurrentDictionary<string, StoredDocumentEntry> DocumentStore = new(
        new Dictionary<string, StoredDocumentEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["doc-seed-coach-01"] = new(
                "doc-seed-coach-01",
                "Titulo_Profesor_Ferrari.pdf",
                "application/pdf",
                862,
                "JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MTIgNzkyXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA1IDAgUiA+PiA+PiAvQ29udGVudHMgNCAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL0xlbmd0aCAyOTAgPj4Kc3RyZWFtCkJUIC9GMSAxNCBUZiA3MiA3NjAgVGQgMTggVEwgKENMVUIgVEVOSVMgQUhPUkEgLSBET0NVTUVOVE8gREUgTVVFU1RSQSkgVGogVCogKFTtdHVsbyBoYWJpbGl0YW50ZTogUHJvZmVzb3IgTmFjaW9uYWwgZGUgVGVuaXMpIFRqIFQqIChUaXR1bGFyOiBHdXN0YXZvIEZlcnJhcmkpIFRqIFQqIChJbnN0aXR1Y2nzbjogQXNvY2lhY2nzbiBBcmdlbnRpbmEgZGUgVGVuaXMgXChBQVRcKSkgVGogVCogKExpY2VuY2lhOiBBQVQtUFJPLTQxMjg3KSBUaiBUKiAoVmVuY2ltaWVudG86IDIwMjgtMTItMzEpIFRqIFQqIEVUCmVuZHN0cmVhbQplbmRvYmoKNSAwIG9iago8PCAvVHlwZSAvRm9udCAvU3VidHlwZSAvVHlwZTEgL0Jhc2VGb250IC9IZWx2ZXRpY2EgL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcgPj4KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAxMTUgMDAwMDAgbiAKMDAwMDAwMDI0MSAwMDAwMCBuIAowMDAwMDAwNTgyIDAwMDAwIG4gCnRyYWlsZXIKPDwgL1NpemUgNiAvUm9vdCAxIDAgUiA+PgpzdGFydHhyZWYKNjc5CiUlRU9GCg=="),
            ["doc-seed-coach-02"] = new(
                "doc-seed-coach-02",
                "Certificado_Entrenadora_Dominguez.pdf",
                "application/pdf",
                873,
                "JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MTIgNzkyXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA1IDAgUiA+PiA+PiAvQ29udGVudHMgNCAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL0xlbmd0aCAzMDEgPj4Kc3RyZWFtCkJUIC9GMSAxNCBUZiA3MiA3NjAgVGQgMTggVEwgKENMVUIgVEVOSVMgQUhPUkEgLSBET0NVTUVOVE8gREUgTVVFU1RSQSkgVGogVCogKFTtdHVsbyBoYWJpbGl0YW50ZTogRW50cmVuYWRvcmEgTmFjaW9uYWwgZGUgVGVuaXMgTml2ZWwgMSkgVGogVCogKFRpdHVsYXI6IExhdXJhIERvbe1uZ3VleikgVGogVCogKEluc3RpdHVjafNuOiBBc29jaWFjafNuIEFyZ2VudGluYSBkZSBUZW5pcyBcKEFBVFwpKSBUaiBUKiAoTGljZW5jaWE6IEFBVC1FTlQtNTA5MzIpIFRqIFQqIChWZW5jaW1pZW50bzogMjAyOS0wMy0zMSkgVGogVCogRVQKZW5kc3RyZWFtCmVuZG9iago1IDAgb2JqCjw8IC9UeXBlIC9Gb250IC9TdWJ0eXBlIC9UeXBlMSAvQmFzZUZvbnQgL0hlbHZldGljYSAvRW5jb2RpbmcgL1dpbkFuc2lFbmNvZGluZyA+PgplbmRvYmoKeHJlZgowIDYKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAgbiAKMDAwMDAwMDExNSAwMDAwMCBuIAowMDAwMDAwMjQxIDAwMDAwIG4gCjAwMDAwMDA1OTMgMDAwMDAgbiAKdHJhaWxlcgo8PCAvU2l6ZSA2IC9Sb290IDEgMCBSID4+CnN0YXJ0eHJlZgo2OTAKJSVFT0YK"),
            ["doc-seed-coach-03"] = new(
                "doc-seed-coach-03",
                "Certificado_Entrenador_Paredes.pdf",
                "application/pdf",
                870,
                "JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MTIgNzkyXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA1IDAgUiA+PiA+PiAvQ29udGVudHMgNCAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL0xlbmd0aCAyOTggPj4Kc3RyZWFtCkJUIC9GMSAxNCBUZiA3MiA3NjAgVGQgMTggVEwgKENMVUIgVEVOSVMgQUhPUkEgLSBET0NVTUVOVE8gREUgTVVFU1RSQSkgVGogVCogKFTtdHVsbyBoYWJpbGl0YW50ZTogRW50cmVuYWRvciBOYWNpb25hbCBkZSBUZW5pcyBOaXZlbCAyKSBUaiBUKiAoVGl0dWxhcjogRGllZ28gUGFyZWRlcykgVGogVCogKEluc3RpdHVjafNuOiBBc29jaWFjafNuIEFyZ2VudGluYSBkZSBUZW5pcyBcKEFBVFwpKSBUaiBUKiAoTGljZW5jaWE6IEFBVC1FTlQtNDc3MTApIFRqIFQqIChWZW5jaW1pZW50bzogMjAyNy0xMi0zMSkgVGogVCogRVQKZW5kc3RyZWFtCmVuZG9iago1IDAgb2JqCjw8IC9UeXBlIC9Gb250IC9TdWJ0eXBlIC9UeXBlMSAvQmFzZUZvbnQgL0hlbHZldGljYSAvRW5jb2RpbmcgL1dpbkFuc2lFbmNvZGluZyA+PgplbmRvYmoKeHJlZgowIDYKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAgbiAKMDAwMDAwMDExNSAwMDAwMCBuIAowMDAwMDAwMjQxIDAwMDAwIG4gCjAwMDAwMDA1OTAgMDAwMDAgbiAKdHJhaWxlcgo8PCAvU2l6ZSA2IC9Sb290IDEgMCBSID4+CnN0YXJ0eHJlZgo2ODcKJSVFT0YK")
        });

    private readonly TenisAhoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public AcademiaDocumentosController(
        TenisAhoraDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    // =========================================================================
    // 1. PROFESORES Y ENTRENADORES (/api/profesores)
    // =========================================================================

    [HttpGet("profesores")]
    public async Task<IActionResult> GetProfesores(CancellationToken cancellationToken)
    {
        var list = await _dbContext.Profesionales
            .AsNoTracking()
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return Ok(list.Select(MapProfesionalDto).ToList());
    }

    [HttpGet("profesores/{id}")]
    public async Task<IActionResult> GetProfesorById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var prof = await _dbContext.Profesionales
            .AsNoTracking()
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prof is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{id}`.");
        }

        return Ok(MapProfesionalDto(prof));
    }

    [HttpPost("profesores")]
    public async Task<IActionResult> CreateProfesor(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        if (!body.TryGetProperty("licenseDetails", out var licElement) || licElement.ValueKind != JsonValueKind.Object)
        {
            throw DomainException.BadRequest("Falta la denominación del título del título habilitante (RD08).");
        }

        var validatedLic = await ValidateAndNormalizeLicenseAsync(licElement, cancellationToken);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"coach-{nowMs}";
        }

        var fullName = GetString(body, "name", "Profesor Tenis Ahora").Trim();
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var nombre = parts.Length > 0 ? parts[0] : "Profesor";
        var apellido = parts.Length > 1 ? parts[1] : "Tenis Ahora";

        var email = GetString(body, "email", $"{id}@tenisahora.com").Trim().ToLowerInvariant();
        var dni = GetString(body, "dni", $"20.{Random.Shared.Next(100, 999)}.{Random.Shared.Next(100, 999)}").Trim();
        var type = NormalizeCoachType(GetString(body, "type", "Profesor"));
        var availableHours = GetString(body, "availableHours", "Lunes a Viernes 09:00 - 18:00");

        var prof = new Profesional
        {
            Id = id,
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = fullName,
            Dni = dni,
            Telefono = GetString(body, "phone"),
            Email = email,
            Rol = "coach",
            Activo = true,
            TipoProfesional = type,
            PoseeLicenciaOficial = true,
            TarifaHoraParticular = 6000m,
            Biografia = availableHours
        };

        var cert = new Certificado
        {
            Id = $"cert-{id}",
            ProfesionalId = id,
            TituloHabilitante = validatedLic.TitleName,
            InstitucionEmisora = validatedLic.Institution,
            NumeroMatricula = validatedLic.LicenseNumber,
            FechaEmision = validatedLic.IssueDate,
            FechaVencimiento = validatedLic.ExpiryDate,
            Estado = validatedLic.Status,
            DocumentoNombre = validatedLic.DocName,
            DocumentoMimeType = validatedLic.DocMimeType,
            DocumentoTamanoBytes = validatedLic.DocSize,
            DocumentoUrlOBase64 = validatedLic.DocBase64
        };

        var horario = new HorarioProfesional
        {
            Id = $"hp-{id}-1",
            ProfesionalId = id,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(20, 0),
            Disponible = true,
            Observaciones = availableHours
        };

        _dbContext.Profesionales.Add(prof);
        _dbContext.Certificados.Add(cert);
        _dbContext.HorariosProfesional.Add(horario);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Profesionales
            .AsNoTracking()
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .FirstAsync(p => p.Id == id, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapProfesionalDto(created));
    }

    [HttpPut("profesores/{id}")]
    public async Task<IActionResult> UpdateProfesor(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var isOwnCoachProfile = _currentUser.IsProfessional &&
                                string.Equals(_currentUser.CoachId, id, StringComparison.OrdinalIgnoreCase);

        if (!_currentUser.IsStaff && !isOwnCoachProfile)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var prof = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prof is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{id}`.");
        }

        if (body.TryGetProperty("name", out _))
        {
            var fullName = GetString(body, "name", prof.NombreCompleto).Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                prof.Nombre = parts[0];
                prof.Apellido = parts.Length > 1 ? parts[1] : string.Empty;
                prof.NombreCompleto = fullName;
            }
        }

        if (body.TryGetProperty("dni", out _) && _currentUser.IsStaff)
        {
            prof.Dni = GetString(body, "dni", prof.Dni);
        }

        if (body.TryGetProperty("phone", out _))
        {
            prof.Telefono = GetString(body, "phone", prof.Telefono ?? string.Empty);
        }

        if (body.TryGetProperty("email", out _) && _currentUser.IsStaff)
        {
            prof.Email = GetString(body, "email", prof.Email).Trim().ToLowerInvariant();
        }

        if (body.TryGetProperty("photo", out _))
        {
            prof.FotoUrl = GetString(body, "photo");
        }

        if (body.TryGetProperty("type", out _) && _currentUser.IsStaff)
        {
            prof.TipoProfesional = NormalizeCoachType(GetString(body, "type", prof.TipoProfesional));
        }

        if (body.TryGetProperty("availableHours", out _))
        {
            var hours = GetString(body, "availableHours");
            UpdateCoachAvailableHoursInternal(prof, hours);
        }

        if (_currentUser.IsStaff &&
            body.TryGetProperty("licenseDetails", out var licElement) &&
            licElement.ValueKind == JsonValueKind.Object)
        {
            var validatedLic = await ValidateAndNormalizeLicenseAsync(licElement, cancellationToken);
            UpsertCoachCertificado(prof, validatedLic);
        }

        prof.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapProfesionalDto(prof));
    }

    [HttpDelete("profesores/{id}")]
    public async Task<IActionResult> DeleteProfesor(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var prof = await _dbContext.Profesionales
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prof is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{id}`.");
        }

        _dbContext.Profesionales.Remove(prof);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// PUT /api/profesores/{id}/disponibilidad — Actualiza las franjas de <see cref="HorarioProfesional"/> del profesor o entrenador.
    /// </summary>
    [HttpPut("profesores/{id}/disponibilidad")]
    public async Task<IActionResult> UpdateProfesorDisponibilidad(
        [FromRoute] string id,
        [FromBody] UpdateCoachAvailabilityRequestDto request,
        CancellationToken cancellationToken)
    {
        var isOwnCoachProfile = _currentUser.IsProfessional &&
                                string.Equals(_currentUser.CoachId, id, StringComparison.OrdinalIgnoreCase);

        if (!_currentUser.IsStaff && !isOwnCoachProfile)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var prof = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prof is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{id}`.");
        }

        UpdateCoachAvailableHoursInternal(prof, request.AvailableHours ?? string.Empty);
        prof.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapProfesionalDto(prof));
    }

    /// <summary>
    /// PUT /api/profesores/{id}/certificacion — Actualiza o crea el <see cref="Certificado"/> habilitante validando RD08.
    /// </summary>
    [HttpPut("profesores/{id}/certificacion")]
    public async Task<IActionResult> UpdateProfesorCertificacion(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var prof = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .Include(p => p.HorariosDisponibles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prof is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{id}`.");
        }

        var validatedLic = await ValidateAndNormalizeLicenseAsync(body, cancellationToken);
        UpsertCoachCertificado(prof, validatedLic);

        prof.PoseeLicenciaOficial = true;
        prof.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapProfesionalDto(prof));
    }

    // =========================================================================
    // 2. CLASES Y ENTRENAMIENTOS GRUPALES TPT (/api/clases)
    // =========================================================================

    [HttpGet("clases")]
    public async Task<IActionResult> GetClases(CancellationToken cancellationToken)
    {
        var list = await QueryAllActividadesGrupalesAsync(cancellationToken);
        var visible = FilterAndMapVisibleClasses(list);
        return Ok(visible);
    }

    [HttpGet("clases/{id}")]
    public async Task<IActionResult> GetClaseById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var act = await _dbContext.ActividadesGrupales
            .AsNoTracking()
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        if (_currentUser.IsProfessional && !_currentUser.IsStaff &&
            !string.Equals(act.ProfesionalId, _currentUser.CoachId, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.Forbidden("No tenés acceso a este registro.");
        }

        return Ok(MapActividadGrupalDto(act, includeStudents: _currentUser.IsStaff || _currentUser.IsProfessional));
    }

    [HttpPost("clases")]
    public async Task<IActionResult> CreateClase(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var name = GetString(body, "name").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre de la comisión es obligatorio.");
        }

        var kind = GetString(body, "kind", "clase").Trim().ToLowerInvariant();
        if (kind is not ("clase" or "entrenamiento"))
        {
            kind = "clase";
        }

        var coachId = GetString(body, "coachId").Trim();
        var courtAssigned = GetString(body, "courtAssigned", "Cancha 1 (Ladrillo)").Trim();
        var maxCapacity = await ValidateClassCapacityAsync(courtAssigned, GetInt(body, "maxCapacity", 0), cancellationToken);

        var coach = await ValidateCoachKindAndQualificationAsync(coachId, kind, cancellationToken);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"cls-{nowMs}";
        }

        ActividadGrupal nuevaActividad = kind == "entrenamiento"
            ? new Entrenamiento
            {
                Id = id,
                ProfesionalId = coach.Id,
                Nombre = name,
                TipoActividad = "entrenamiento",
                NombreProfesional = coach.NombreCompleto,
                CanchaAsignada = courtAssigned,
                DiasSemana = GetString(body, "scheduleDays", "Lunes y Miércoles"),
                HorarioFranja = GetString(body, "scheduleTime", "18:00 - 19:30"),
                CupoMaximo = maxCapacity,
                InscriptosActuales = 0,
                ArancelMensual = Math.Max(GetDecimal(body, "monthlyFee", 15000m), 0m),
                PeriodoMes = GetString(body, "monthName", "Octubre 2026"),
                Estado = "activa",
                EnfoqueEntrenamiento = "Físico-Técnico",
                NivelIntensidad = "Media-Alta"
            }
            : new Clase
            {
                Id = id,
                ProfesionalId = coach.Id,
                Nombre = name,
                TipoActividad = "clase",
                NombreProfesional = coach.NombreCompleto,
                CanchaAsignada = courtAssigned,
                DiasSemana = GetString(body, "scheduleDays", "Martes y Jueves"),
                HorarioFranja = GetString(body, "scheduleTime", "19:00 - 20:30"),
                CupoMaximo = maxCapacity,
                InscriptosActuales = 0,
                ArancelMensual = Math.Max(GetDecimal(body, "monthlyFee", 16000m), 0m),
                PeriodoMes = GetString(body, "monthName", "Octubre 2026"),
                Estado = "activa",
                NivelEscuela = "Intermedio",
                RangoEdad = "Adultos"
            };

        _dbContext.ActividadesGrupales.Add(nuevaActividad);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapActividadGrupalDto(nuevaActividad, includeStudents: true));
    }

    [HttpPut("clases/{id}")]
    public async Task<IActionResult> UpdateClase(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        var nextKind = GetString(body, "kind", act.TipoActividad).Trim().ToLowerInvariant();
        var nextCoachId = GetString(body, "coachId", act.ProfesionalId).Trim();
        var nextCourt = GetString(body, "courtAssigned", act.CanchaAsignada ?? string.Empty).Trim();
        var rawCapacity = body.TryGetProperty("maxCapacity", out _)
            ? GetInt(body, "maxCapacity", act.CupoMaximo)
            : act.CupoMaximo;

        var validatedCapacity = await ValidateClassCapacityAsync(nextCourt, rawCapacity, cancellationToken);

        var coach = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .FirstOrDefaultAsync(p => p.Id == nextCoachId, cancellationToken);

        if (coach is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{nextCoachId}`.");
        }

        var expectedKind = string.Equals(coach.TipoProfesional, "Entrenador", StringComparison.OrdinalIgnoreCase)
            ? "entrenamiento"
            : "clase";

        if (!string.Equals(nextKind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest(
                $"{coach.NombreCompleto} es {coach.TipoProfesional.ToLowerInvariant()}: solo puede tener {expectedKind}s.");
        }

        if (!string.Equals(nextCoachId, act.ProfesionalId, StringComparison.OrdinalIgnoreCase))
        {
            EnsureCoachQualified(coach);
        }

        act.Nombre = GetString(body, "name", act.Nombre).Trim();
        act.TipoActividad = nextKind;
        act.ProfesionalId = coach.Id;
        act.NombreProfesional = coach.NombreCompleto;
        act.CanchaAsignada = nextCourt;
        act.CupoMaximo = validatedCapacity;

        if (body.TryGetProperty("scheduleDays", out _))
        {
            act.DiasSemana = GetString(body, "scheduleDays", act.DiasSemana);
        }
        if (body.TryGetProperty("scheduleTime", out _))
        {
            act.HorarioFranja = GetString(body, "scheduleTime", act.HorarioFranja);
        }
        if (body.TryGetProperty("monthlyFee", out _))
        {
            act.ArancelMensual = Math.Max(GetDecimal(body, "monthlyFee", act.ArancelMensual), 0m);
        }
        if (body.TryGetProperty("monthName", out _))
        {
            act.PeriodoMes = GetString(body, "monthName", act.PeriodoMes ?? "Octubre 2026");
        }

        act.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapActividadGrupalDto(act, includeStudents: true));
    }

    [HttpDelete("clases/{id}")]
    public async Task<IActionResult> DeleteClase(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        EnsureStaff();

        var act = await _dbContext.ActividadesGrupales
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        _dbContext.ActividadesGrupales.Remove(act);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    // =========================================================================
    // 3. ALUMNOS Y ASISTENCIA EN CLASES (/api/clases/{id}/alumnos y /asistencia)
    // =========================================================================

    [HttpPost("clases/{id}/alumnos")]
    public async Task<IActionResult> EnrollStudentInClass(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        var studentName = GetString(body, "name").Trim();
        var studentDni = GetString(body, "dni", _currentUser.Dni).Trim();
        var studentPhone = GetString(body, "phone");

        if (_currentUser.IsMember && !_currentUser.IsStaff)
        {
            if (!_currentUser.IsMeByDni(studentDni))
            {
                throw DomainException.Forbidden("Solo podés inscribirte a vos mismo.");
            }
        }
        else if (!_currentUser.IsStaff)
        {
            EnsureClassTeacher(act);
        }

        if (string.IsNullOrWhiteSpace(studentName))
        {
            throw DomainException.BadRequest("El nombre del alumno es obligatorio.");
        }

        var activeStudents = act.Inscripciones.Where(i => i.Estado == "activa").ToList();
        var capacity = Math.Min(act.CupoMaximo <= 0 ? 30 : act.CupoMaximo, 30);
        if (activeStudents.Count >= capacity)
        {
            throw DomainException.Conflict(
                $"No es posible agregar más alumnos: el cupo de {capacity} está completo.");
        }

        var cliente = await ResolveOrCreateClienteAsync(studentDni, studentName, studentPhone, cancellationToken);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var inscId = $"std-{nowMs}";

        var sessions = ExtractSessionLabels(act);

        var insc = new InscripcionActividad
        {
            Id = inscId,
            ActividadGrupalId = act.Id,
            ClienteId = cliente.Id,
            NombreAlumno = studentName,
            DniAlumno = string.IsNullOrWhiteSpace(studentDni) ? cliente.Dni : studentDni,
            TelefonoAlumno = studentPhone,
            FechaInscripcion = DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = "activa"
        };

        _dbContext.InscripcionesActividad.Add(insc);

        for (var i = 0; i < sessions.Count; i++)
        {
            var asist = new Asistencia
            {
                Id = $"ast-{inscId}-{i + 1}",
                ActividadGrupalId = act.Id,
                InscripcionActividadId = inscId,
                ClienteId = cliente.Id,
                NumeroSesion = i + 1,
                EtiquetaSesion = sessions[i],
                FechaSesion = DateOnly.FromDateTime(DateTime.UtcNow),
                EstadoAsistencia = "P"
            };
            insc.Asistencias.Add(asist);
            _dbContext.Asistencias.Add(asist);
        }

        act.InscriptosActuales = activeStudents.Count + 1;
        act.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            student = MapStudentDto(insc, sessions.Count)
        });
    }

    [HttpDelete("clases/{id}/alumnos/{studentId}")]
    public async Task<IActionResult> UnenrollStudentFromClass(
        [FromRoute] string id,
        [FromRoute] string studentId,
        CancellationToken cancellationToken)
    {
        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        EnsureClassTeacher(act);

        var insc = act.Inscripciones.FirstOrDefault(i => i.Id == studentId);
        if (insc is not null)
        {
            _dbContext.InscripcionesActividad.Remove(insc);
            act.InscriptosActuales = Math.Max(act.Inscripciones.Count(i => i.Id != studentId && i.Estado == "activa"), 0);
            act.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// POST /api/clases/{id}/asistencia/alternar — Alterna la asistencia (<c>P</c> &lt;-&gt; <c>A</c>) de un alumno en una sesión.
    /// </summary>
    [HttpPost("clases/{id}/asistencia/alternar")]
    public async Task<IActionResult> ToggleClassAttendance(
        [FromRoute] string id,
        [FromBody] ToggleAttendanceRequestDto request,
        CancellationToken cancellationToken)
    {
        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        EnsureClassTeacher(act);

        var studentId = request.StudentId ?? string.Empty;
        var sessionIdx = Math.Max(request.SessionIndex, 0);
        var sessionNumber = sessionIdx + 1;

        var insc = act.Inscripciones.FirstOrDefault(i => i.Id == studentId);
        if (insc is not null)
        {
            var existingAst = insc.Asistencias.FirstOrDefault(a => a.NumeroSesion == sessionNumber);
            if (existingAst is not null)
            {
                existingAst.EstadoAsistencia = existingAst.EstadoAsistencia == "P" ? "A" : "P";
                existingAst.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var sessions = ExtractSessionLabels(act);
                var label = sessionIdx < sessions.Count ? sessions[sessionIdx] : $"S{sessionNumber}";
                _dbContext.Asistencias.Add(new Asistencia
                {
                    Id = $"ast-{insc.Id}-{sessionNumber}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    ActividadGrupalId = act.Id,
                    InscripcionActividadId = insc.Id,
                    ClienteId = insc.ClienteId,
                    NumeroSesion = sessionNumber,
                    EtiquetaSesion = label,
                    FechaSesion = DateOnly.FromDateTime(DateTime.UtcNow),
                    EstadoAsistencia = "P"
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var allClasses = await QueryAllActividadesGrupalesAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            classes = FilterAndMapVisibleClasses(allClasses)
        });
    }

    /// <summary>
    /// POST /api/clases/{id}/asistencia/sesion — Registra en bloque la asistencia (<c>P</c> o <c>A</c>) de todos los alumnos en una sesión.
    /// </summary>
    [HttpPost("clases/{id}/asistencia/sesion")]
    public async Task<IActionResult> BulkSessionAttendance(
        [FromRoute] string id,
        [FromBody] BulkSessionAttendanceRequestDto request,
        CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(request.Status) ? "P" : request.Status.Trim().ToUpperInvariant();
        if (status is not ("P" or "A"))
        {
            throw DomainException.BadRequest("El estado de asistencia debe ser P (presente) o A (ausente).");
        }

        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        EnsureClassTeacher(act);

        var sessionIdx = Math.Max(request.SessionIndex, 0);
        var sessionNumber = sessionIdx + 1;
        var sessions = ExtractSessionLabels(act);
        var label = sessionIdx < sessions.Count ? sessions[sessionIdx] : $"S{sessionNumber}";

        foreach (var insc in act.Inscripciones.Where(i => i.Estado == "activa"))
        {
            var existingAst = insc.Asistencias.FirstOrDefault(a => a.NumeroSesion == sessionNumber);
            if (existingAst is not null)
            {
                existingAst.EstadoAsistencia = status;
                existingAst.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _dbContext.Asistencias.Add(new Asistencia
                {
                    Id = $"ast-{insc.Id}-{sessionNumber}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    ActividadGrupalId = act.Id,
                    InscripcionActividadId = insc.Id,
                    ClienteId = insc.ClienteId,
                    NumeroSesion = sessionNumber,
                    EtiquetaSesion = label,
                    FechaSesion = DateOnly.FromDateTime(DateTime.UtcNow),
                    EstadoAsistencia = status
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var allClasses = await QueryAllActividadesGrupalesAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            classes = FilterAndMapVisibleClasses(allClasses)
        });
    }

    /// <summary>
    /// POST /api/clases/{id}/asistencia/registrar — Agrega un nuevo registro de sesión de asistencia al alumno.
    /// </summary>
    [HttpPost("clases/{id}/asistencia/registrar")]
    public async Task<IActionResult> RecordStudentAttendance(
        [FromRoute] string id,
        [FromBody] RecordStudentAttendanceRequestDto request,
        CancellationToken cancellationToken)
    {
        var status = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (status is not ("P" or "A"))
        {
            throw DomainException.BadRequest("El estado de asistencia debe ser P (presente) o A (ausente).");
        }

        var act = await _dbContext.ActividadesGrupales
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases` el registro con id `{id}`.");
        }

        EnsureClassTeacher(act);

        var insc = act.Inscripciones.FirstOrDefault(i => i.Id == request.StudentId);
        if (insc is not null)
        {
            var nextSessionNum = insc.Asistencias.Count > 0
                ? insc.Asistencias.Max(a => a.NumeroSesion) + 1
                : 1;

            _dbContext.Asistencias.Add(new Asistencia
            {
                Id = $"ast-{insc.Id}-{nextSessionNum}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                ActividadGrupalId = act.Id,
                InscripcionActividadId = insc.Id,
                ClienteId = insc.ClienteId,
                NumeroSesion = nextSessionNum,
                EtiquetaSesion = DateOnly.FromDateTime(DateTime.UtcNow).ToString("dd/MM"),
                FechaSesion = DateOnly.FromDateTime(DateTime.UtcNow),
                EstadoAsistencia = status
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true });
    }

    // =========================================================================
    // 4. CLASES Y ENTRENAMIENTOS PARTICULARES (/api/clases-particulares)
    // =========================================================================

    [HttpGet("clases-particulares")]
    public async Task<IActionResult> GetClasesParticulares(CancellationToken cancellationToken)
    {
        var query = _dbContext.TurnosParticulares.AsNoTracking();

        if (!_currentUser.IsStaff)
        {
            if (_currentUser.IsProfessional)
            {
                var coachId = _currentUser.CoachId ?? _currentUser.UserId;
                query = query.Where(t => t.ProfesionalId == coachId);
            }
            else if (_currentUser.IsMember)
            {
                var dni = _currentUser.Dni;
                query = query.Where(t => t.DniAlumno == dni);
            }
        }

        var list = await query.OrderBy(t => t.Id).ToListAsync(cancellationToken);
        return Ok(list.Select(MapTurnoParticularDto).ToList());
    }

    [HttpGet("clases-particulares/{id}")]
    public async Task<IActionResult> GetClaseParticularById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var turno = await _dbContext.TurnosParticulares
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (turno is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases-particulares` el registro con id `{id}`.");
        }

        if (!CanAccessPrivateLesson(turno))
        {
            throw DomainException.Forbidden("No tenés acceso a este registro.");
        }

        return Ok(MapTurnoParticularDto(turno));
    }

    [HttpPost("clases-particulares")]
    public async Task<IActionResult> CreateClaseParticular(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var studentDni = GetString(body, "studentDni", _currentUser.Dni).Trim();
        var coachId = GetString(body, "coachId").Trim();
        var kind = GetString(body, "kind", "clase").Trim().ToLowerInvariant();

        if (!_currentUser.IsStaff)
        {
            if (_currentUser.IsProfessional)
            {
                coachId = _currentUser.CoachId ?? _currentUser.UserId;
            }
            else if (_currentUser.IsMember)
            {
                if (!_currentUser.IsMeByDni(studentDni))
                {
                    throw DomainException.Forbidden("Solo podés pedir clases particulares para vos.");
                }
            }
            else
            {
                throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
            }
        }

        var coach = await ValidateCoachKindAndQualificationAsync(coachId, kind, cancellationToken);

        var date = ParseDateOrDefault(GetString(body, "date"), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
        var timeSlot = GetString(body, "time", "10:00 - 11:00").Trim();
        var (startTime, endTime) = ParseTimeRange(timeSlot);

        // Validar no solapamiento de profesor ni de alumno en la misma fecha y horario
        var overlap = await _dbContext.TurnosParticulares
            .AsNoTracking()
            .AnyAsync(t => t.Fecha == date
                           && t.Estado != "cancelada"
                           && startTime < t.HoraFin
                           && endTime > t.HoraInicio
                           && (t.ProfesionalId == coach.Id || t.DniAlumno == studentDni),
                cancellationToken);

        if (overlap)
        {
            throw DomainException.Conflict(
                "Ya existe un turno particular activo para el profesional o el alumno en ese horario.");
        }

        var studentName = GetString(body, "studentName", "Socio Tenis Ahora").Trim();
        var studentPhone = GetString(body, "studentPhone");
        var cliente = await ResolveOrCreateClienteAsync(studentDni, studentName, studentPhone, cancellationToken);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"prv-{nowMs}";
        }

        var turno = new TurnoParticular
        {
            Id = id,
            ProfesionalId = coach.Id,
            ClienteId = cliente.Id,
            Modalidad = kind,
            NombreProfesional = GetString(body, "coachName", coach.NombreCompleto),
            NombreAlumno = studentName,
            DniAlumno = string.IsNullOrWhiteSpace(studentDni) ? cliente.Dni : studentDni,
            TelefonoAlumno = studentPhone,
            CanchaAsignada = GetString(body, "courtAssigned", "Cancha 1 (Ladrillo)"),
            Fecha = date,
            FranjaHoraria = timeSlot,
            HoraInicio = startTime,
            HoraFin = endTime,
            TarifaHora = Math.Max(GetDecimal(body, "hourlyRate", coach.TarifaHoraParticular > 0 ? coach.TarifaHoraParticular : 6000m), 0m),
            Estado = GetString(body, "status", "programada"),
            Notas = GetString(body, "notes")
        };

        _dbContext.TurnosParticulares.Add(turno);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapTurnoParticularDto(turno));
    }

    [HttpPut("clases-particulares/{id}")]
    public async Task<IActionResult> UpdateClaseParticular(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var turno = await _dbContext.TurnosParticulares
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (turno is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases-particulares` el registro con id `{id}`.");
        }

        var isTeacher = _currentUser.IsProfessional &&
                        string.Equals(_currentUser.CoachId, turno.ProfesionalId, StringComparison.OrdinalIgnoreCase);

        if (!_currentUser.IsStaff && !isTeacher)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var nextCoachId = isTeacher
            ? turno.ProfesionalId
            : GetString(body, "coachId", turno.ProfesionalId).Trim();
        var nextKind = GetString(body, "kind", turno.Modalidad).Trim().ToLowerInvariant();

        var coach = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .FirstOrDefaultAsync(p => p.Id == nextCoachId, cancellationToken);

        if (coach is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{nextCoachId}`.");
        }

        var expectedKind = string.Equals(coach.TipoProfesional, "Entrenador", StringComparison.OrdinalIgnoreCase)
            ? "entrenamiento"
            : "clase";

        if (!string.Equals(nextKind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest(
                $"{coach.NombreCompleto} es {coach.TipoProfesional.ToLowerInvariant()}: solo puede tener {expectedKind}s.");
        }

        if (!string.Equals(nextCoachId, turno.ProfesionalId, StringComparison.OrdinalIgnoreCase))
        {
            EnsureCoachQualified(coach);
        }

        turno.ProfesionalId = coach.Id;
        turno.NombreProfesional = GetString(body, "coachName", coach.NombreCompleto);
        turno.Modalidad = nextKind;

        if (body.TryGetProperty("studentName", out _))
        {
            turno.NombreAlumno = GetString(body, "studentName", turno.NombreAlumno);
        }
        if (body.TryGetProperty("studentDni", out _))
        {
            turno.DniAlumno = GetString(body, "studentDni", turno.DniAlumno);
        }
        if (body.TryGetProperty("studentPhone", out _))
        {
            turno.TelefonoAlumno = GetString(body, "studentPhone", turno.TelefonoAlumno ?? string.Empty);
        }
        if (body.TryGetProperty("date", out _))
        {
            turno.Fecha = ParseDateOrDefault(GetString(body, "date"), turno.Fecha);
        }
        if (body.TryGetProperty("time", out _))
        {
            turno.FranjaHoraria = GetString(body, "time", turno.FranjaHoraria);
            var (st, et) = ParseTimeRange(turno.FranjaHoraria);
            turno.HoraInicio = st;
            turno.HoraFin = et;
        }
        if (body.TryGetProperty("courtAssigned", out _))
        {
            turno.CanchaAsignada = GetString(body, "courtAssigned", turno.CanchaAsignada ?? string.Empty);
        }
        if (body.TryGetProperty("hourlyRate", out _))
        {
            turno.TarifaHora = Math.Max(GetDecimal(body, "hourlyRate", turno.TarifaHora), 0m);
        }
        if (body.TryGetProperty("status", out _))
        {
            turno.Estado = GetString(body, "status", turno.Estado);
        }
        if (body.TryGetProperty("notes", out _))
        {
            turno.Notas = GetString(body, "notes");
        }

        turno.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapTurnoParticularDto(turno));
    }

    [HttpDelete("clases-particulares/{id}")]
    public async Task<IActionResult> DeleteClaseParticular(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var turno = await _dbContext.TurnosParticulares
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (turno is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases-particulares` el registro con id `{id}`.");
        }

        var isTeacher = _currentUser.IsProfessional &&
                        string.Equals(_currentUser.CoachId, turno.ProfesionalId, StringComparison.OrdinalIgnoreCase);

        if (!_currentUser.IsStaff && !isTeacher)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        _dbContext.TurnosParticulares.Remove(turno);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// POST /api/clases-particulares/{id}/cancelar — Cancela un turno particular.
    /// </summary>
    [HttpPost("clases-particulares/{id}/cancelar")]
    public async Task<IActionResult> CancelClaseParticular(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var turno = await _dbContext.TurnosParticulares
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (turno is null)
        {
            throw DomainException.NotFound($"No se encontró en `clases-particulares` el registro con id `{id}`.");
        }

        if (!CanAccessPrivateLesson(turno))
        {
            throw DomainException.Forbidden("La clase particular no es tuya.");
        }

        turno.Estado = "cancelada";
        turno.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = MapTurnoParticularDto(turno)
        });
    }

    // =========================================================================
    // 5. SUBIDA Y DESCARGA DE DOCUMENTOS (/api/documentos)
    // =========================================================================

    /// <summary>
    /// POST /api/documentos — Subida de documentos digitales en base64 (máx. 5 MB, PDF/JPG/PNG) para títulos habilitantes (RD08) y reglamentos (RD12).
    /// </summary>
    [HttpPost("documentos")]
    public IActionResult UploadDocument([FromBody] UploadDocumentRequestDto request)
    {
        EnsureStaff();

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.BadRequest("El nombre del archivo es obligatorio.");
        }

        var base64Data = (request.Data ?? string.Empty).Trim();
        var commaIndex = base64Data.IndexOf(',');
        if (commaIndex >= 0)
        {
            base64Data = base64Data[(commaIndex + 1)..];
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(base64Data);
        }
        catch
        {
            throw DomainException.BadRequest("El contenido del archivo no es base64 válido.");
        }

        if (rawBytes.Length == 0)
        {
            throw DomainException.BadRequest("El contenido del archivo no es base64 válido.");
        }

        if (rawBytes.Length > MaxDocumentBytes)
        {
            throw DomainException.BadRequest("El archivo supera el máximo de 5 MB.");
        }

        var mimeType = DetectAllowedDocumentMimeType(rawBytes);
        if (mimeType is null)
        {
            throw DomainException.BadRequest("Solo se aceptan archivos PDF, JPG o PNG.");
        }

        var id = $"doc-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var entry = new StoredDocumentEntry(id, name, mimeType, rawBytes.Length, base64Data);
        DocumentStore[id] = entry;

        return StatusCode(StatusCodes.Status201Created, new
        {
            id = entry.Id,
            name = entry.Name,
            mimeType = entry.MimeType,
            size = entry.Size
        });
    }

    /// <summary>
    /// GET /api/documentos/{id} — Descarga binaria (<c>application/pdf</c>, <c>image/jpeg</c>, <c>image/png</c>) de un documento.
    /// </summary>
    [HttpGet("documentos/{id}")]
    public async Task<IActionResult> DownloadDocument(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var resolved = await TryResolveDocumentAsync(_dbContext, id, cancellationToken);
        if (resolved is null)
        {
            throw DomainException.NotFound($"No se encontró en `documentos` el registro con id `{id}`.");
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(resolved.Value.Base64Data);
        }
        catch
        {
            throw DomainException.BadRequest("El documento guardado está dañado.");
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{resolved.Value.Name}\"";
        return File(rawBytes, resolved.Value.MimeType);
    }

    internal static async Task<StoredDocumentEntry?> TryResolveDocumentAsync(
        TenisAhoraDbContext dbContext,
        string docId,
        CancellationToken cancellationToken)
    {
        if (DocumentStore.TryGetValue(docId, out var memDoc))
        {
            return memDoc;
        }

        var cert = await dbContext.Certificados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == docId || $"doc-{c.Id}" == docId || $"doc-seed-{c.ProfesionalId}" == docId, cancellationToken);

        if (cert is not null && !string.IsNullOrWhiteSpace(cert.DocumentoNombre))
        {
            var base64 = !string.IsNullOrWhiteSpace(cert.DocumentoUrlOBase64)
                ? cert.DocumentoUrlOBase64
                : DocumentStore["doc-seed-coach-01"].Base64Data;

            return new StoredDocumentEntry(
                docId,
                cert.DocumentoNombre,
                cert.DocumentoMimeType ?? "application/pdf",
                cert.DocumentoTamanoBytes ?? 862,
                base64);
        }

        var reg = await dbContext.Reglamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == docId || $"doc-reg-{r.Id}" == docId, cancellationToken);

        if (reg is not null && !string.IsNullOrWhiteSpace(reg.DocumentoNombre))
        {
            var base64 = !string.IsNullOrWhiteSpace(reg.DocumentoBase64OUrl)
                ? reg.DocumentoBase64OUrl
                : DocumentStore["doc-seed-coach-01"].Base64Data;

            return new StoredDocumentEntry(
                docId,
                reg.DocumentoNombre,
                reg.DocumentoMimeType ?? "application/pdf",
                reg.DocumentoTamanoBytes ?? 862,
                base64);
        }

        return null;
    }

    // =========================================================================
    // MÉTODOS AUXILIARES PRIVADOS (RD07, RD08, RD09, RD10)
    // =========================================================================

    private void EnsureStaff()
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }
    }

    private void EnsureClassTeacher(ActividadGrupal act)
    {
        if (_currentUser.IsStaff)
        {
            return;
        }

        if (_currentUser.IsProfessional &&
            string.Equals(_currentUser.CoachId, act.ProfesionalId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw DomainException.Forbidden("La clase no está a tu cargo.");
    }

    private bool CanAccessPrivateLesson(TurnoParticular turno)
    {
        if (_currentUser.IsStaff)
        {
            return true;
        }
        if (_currentUser.IsProfessional &&
            string.Equals(_currentUser.CoachId, turno.ProfesionalId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (_currentUser.IsMember && _currentUser.IsMeByDni(turno.DniAlumno))
        {
            return true;
        }
        return false;
    }

    private async Task<int> ValidateClassCapacityAsync(
        string courtAssigned,
        int requestedCapacity,
        CancellationToken cancellationToken)
    {
        var limit = 30;
        var openIdx = courtAssigned.LastIndexOf('(');
        var closeIdx = courtAssigned.LastIndexOf(')');
        if (openIdx >= 0 && closeIdx > openIdx)
        {
            var surface = courtAssigned[(openIdx + 1)..closeIdx].Trim();
            var tipo = await _dbContext.TiposCancha
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Nombre.ToLower() == surface.ToLower(), cancellationToken);

            if (tipo is not null && tipo.MaxAlumnosPorClase > 0)
            {
                limit = Math.Min(tipo.MaxAlumnosPorClase, 30);
            }
        }

        if (requestedCapacity > 30)
        {
            throw DomainException.BadRequest("El cupo reglamentario máximo es de hasta 30 alumnos.");
        }

        if (requestedCapacity > limit)
        {
            throw DomainException.BadRequest($"El cupo máximo para {courtAssigned} es de {limit} alumnos (RD10).");
        }

        return requestedCapacity <= 0 ? limit : requestedCapacity;
    }

    private async Task<Profesional> ValidateCoachKindAndQualificationAsync(
        string coachId,
        string kind,
        CancellationToken cancellationToken)
    {
        var coach = await _dbContext.Profesionales
            .Include(p => p.Certificados)
            .FirstOrDefaultAsync(p => p.Id == coachId, cancellationToken);

        if (coach is null)
        {
            throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{coachId}`.");
        }

        var expectedKind = string.Equals(coach.TipoProfesional, "Entrenador", StringComparison.OrdinalIgnoreCase)
            ? "entrenamiento"
            : "clase";

        if (!string.Equals(kind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest(
                $"{coach.NombreCompleto} es {coach.TipoProfesional.ToLowerInvariant()}: solo puede tener {expectedKind}s.");
        }

        EnsureCoachQualified(coach);
        return coach;
    }

    private static void EnsureCoachQualified(Profesional coach)
    {
        var cert = coach.Certificados
            .OrderByDescending(c => c.FechaVencimiento)
            .FirstOrDefault();

        if (cert is null || string.IsNullOrWhiteSpace(cert.DocumentoNombre))
        {
            throw DomainException.BadRequest($"{coach.NombreCompleto} no tiene cargado el documento del título habilitante (RD08).");
        }

        if (!string.Equals(cert.Estado, "habilitado", StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.BadRequest($"El título habilitante de {coach.NombreCompleto} no está habilitado (RD09).");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (cert.FechaVencimiento < today)
        {
            throw DomainException.BadRequest(
                $"El título habilitante de {coach.NombreCompleto} venció el {cert.FechaVencimiento:yyyy-MM-dd} (RD09).");
        }
    }

    private async Task<ValidatedLicenseInfo> ValidateAndNormalizeLicenseAsync(
        JsonElement licElement,
        CancellationToken cancellationToken)
    {
        var titleName = GetString(licElement, "titleName").Trim();
        if (string.IsNullOrWhiteSpace(titleName))
        {
            throw DomainException.BadRequest("Falta la denominación del título del título habilitante (RD08).");
        }

        var institution = GetString(licElement, "institution").Trim();
        if (string.IsNullOrWhiteSpace(institution))
        {
            throw DomainException.BadRequest("Falta la institución emisora del título habilitante (RD08).");
        }

        var licenseNumber = GetString(licElement, "licenseNumber").Trim();
        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            throw DomainException.BadRequest("Falta el número de matrícula o licencia del título habilitante (RD08).");
        }

        var expiryRaw = GetString(licElement, "expiryDate").Trim();
        if (string.IsNullOrWhiteSpace(expiryRaw))
        {
            throw DomainException.BadRequest("Falta la fecha de vencimiento del título habilitante (RD08).");
        }

        if (!DateOnly.TryParseExact(expiryRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiryDate))
        {
            throw DomainException.BadRequest("La fecha de vencimiento del título no es válida (formato AAAA-MM-DD).");
        }

        var issueDate = ParseDateOrDefault(GetString(licElement, "issueDate"), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)));
        var status = GetString(licElement, "status", "habilitado").Trim();
        if (string.IsNullOrWhiteSpace(status))
        {
            status = "habilitado";
        }

        if (!licElement.TryGetProperty("document", out var docElement) || docElement.ValueKind != JsonValueKind.Object)
        {
            throw DomainException.BadRequest("Falta adjuntar el documento del título habilitante (RD08).");
        }

        var docId = GetString(docElement, "id").Trim();
        if (string.IsNullOrWhiteSpace(docId))
        {
            throw DomainException.BadRequest("Falta adjuntar el documento del título habilitante (RD08).");
        }

        var resolved = await TryResolveDocumentAsync(_dbContext, docId, cancellationToken);
        if (resolved is null)
        {
            var fallbackName = GetString(docElement, "name");
            if (string.IsNullOrWhiteSpace(fallbackName))
            {
                throw DomainException.BadRequest($"El documento {docId} no existe.");
            }
            resolved = new StoredDocumentEntry(
                docId,
                fallbackName,
                GetString(docElement, "mimeType", "application/pdf"),
                GetInt(docElement, "size", 862),
                DocumentStore["doc-seed-coach-01"].Base64Data);
        }

        return new ValidatedLicenseInfo(
            titleName,
            institution,
            licenseNumber,
            issueDate,
            expiryDate,
            status,
            resolved.Value.Id,
            resolved.Value.Name,
            resolved.Value.MimeType,
            resolved.Value.Size,
            resolved.Value.Base64Data);
    }

    private void UpsertCoachCertificado(Profesional prof, ValidatedLicenseInfo lic)
    {
        var cert = prof.Certificados.FirstOrDefault();
        if (cert is null)
        {
            cert = new Certificado
            {
                Id = $"cert-{prof.Id}",
                ProfesionalId = prof.Id
            };
            prof.Certificados.Add(cert);
            _dbContext.Certificados.Add(cert);
        }

        cert.TituloHabilitante = lic.TitleName;
        cert.InstitucionEmisora = lic.Institution;
        cert.NumeroMatricula = lic.LicenseNumber;
        cert.FechaEmision = lic.IssueDate;
        cert.FechaVencimiento = lic.ExpiryDate;
        cert.Estado = lic.Status;
        cert.DocumentoNombre = lic.DocName;
        cert.DocumentoMimeType = lic.DocMimeType;
        cert.DocumentoTamanoBytes = lic.DocSize;
        cert.DocumentoUrlOBase64 = lic.DocBase64;
        cert.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void UpdateCoachAvailableHoursInternal(Profesional prof, string availableHours)
    {
        prof.Biografia = availableHours;
        var firstSchedule = prof.HorariosDisponibles.FirstOrDefault();
        if (firstSchedule is not null)
        {
            firstSchedule.Observaciones = availableHours;
            firstSchedule.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var newHp = new HorarioProfesional
            {
                Id = $"hp-{prof.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                ProfesionalId = prof.Id,
                DiaSemana = "Lunes",
                HoraInicio = new TimeOnly(8, 0),
                HoraFin = new TimeOnly(20, 0),
                Disponible = true,
                Observaciones = availableHours
            };
            prof.HorariosDisponibles.Add(newHp);
            _dbContext.HorariosProfesional.Add(newHp);
        }
    }

    private async Task<List<ActividadGrupal>> QueryAllActividadesGrupalesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ActividadesGrupales
            .AsNoTracking()
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Asistencias)
            .Include(a => a.Asistencias)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    private List<Dictionary<string, object?>> FilterAndMapVisibleClasses(List<ActividadGrupal> all)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var act in all)
        {
            if (_currentUser.IsStaff)
            {
                result.Add(MapActividadGrupalDto(act, includeStudents: true));
            }
            else if (_currentUser.IsProfessional)
            {
                if (string.Equals(act.ProfesionalId, _currentUser.CoachId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(MapActividadGrupalDto(act, includeStudents: true));
                }
            }
            else
            {
                result.Add(MapActividadGrupalDto(act, includeStudents: false));
            }
        }
        return result;
    }

    private async Task<Cliente> ResolveOrCreateClienteAsync(
        string dni,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var normalizedDni = string.IsNullOrWhiteSpace(dni) ? "00.000.000" : dni.Trim();
        var existing = await _dbContext.Clientes
            .FirstOrDefaultAsync(c => c.Dni == normalizedDni, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var existingUsuario = await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Dni == normalizedDni, cancellationToken);

        if (existingUsuario is not null)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO public.clientes (id, numero_socio, fecha_alta_socio, total_reservas, saldo_credito_favor) VALUES ({existingUsuario.Id}, {$"TA-{existingUsuario.Id}"}, CURRENT_DATE, 0, 0) ON CONFLICT (id) DO NOTHING",
                cancellationToken);
            return await _dbContext.Clientes.FirstAsync(c => c.Id == existingUsuario.Id, cancellationToken);
        }

        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var nombre = parts.Length > 0 ? parts[0] : "Alumno";
        var apellido = parts.Length > 1 ? parts[1] : "Tenis Ahora";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var nuevo = new Cliente
        {
            Id = $"usr-{nowMs}",
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = fullName,
            Dni = normalizedDni,
            Telefono = phone,
            Email = $"alumno.{nowMs}@tenisahora.com",
            Rol = "client",
            Activo = true,
            NumeroSocio = $"TA-{1000 + (nowMs % 9000)}",
            FechaAltaSocio = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _dbContext.Clientes.Add(nuevo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return nuevo;
    }

    private static object MapProfesionalDto(Profesional p)
    {
        var cert = p.Certificados
            .OrderByDescending(c => c.FechaVencimiento)
            .FirstOrDefault();

        var availableHours = !string.IsNullOrWhiteSpace(p.Biografia)
            ? p.Biografia
            : p.HorariosDisponibles.FirstOrDefault()?.Observaciones
              ?? "Lunes a Viernes 08:00 - 20:00";

        var docId = cert is not null && DocumentStore.ContainsKey($"doc-seed-{p.Id}")
            ? $"doc-seed-{p.Id}"
            : cert is not null
                ? $"doc-{cert.Id}"
                : $"doc-seed-{p.Id}";

        var licenseDetails = cert is not null
            ? new
            {
                titleName = cert.TituloHabilitante,
                institution = cert.InstitucionEmisora,
                licenseNumber = cert.NumeroMatricula,
                issueDate = cert.FechaEmision.ToString("yyyy-MM-dd"),
                expiryDate = cert.FechaVencimiento.ToString("yyyy-MM-dd"),
                status = cert.Estado,
                document = new
                {
                    id = docId,
                    name = cert.DocumentoNombre ?? $"Titulo_{p.Apellido}.pdf",
                    mimeType = cert.DocumentoMimeType ?? "application/pdf",
                    size = cert.DocumentoTamanoBytes ?? 862
                }
            }
            : null;

        return new
        {
            id = p.Id,
            name = p.NombreCompleto,
            type = p.TipoProfesional,
            dni = p.Dni,
            phone = p.Telefono ?? string.Empty,
            email = p.Email,
            photo = p.FotoUrl ?? string.Empty,
            hasOfficialLicense = p.PoseeLicenciaOficial,
            availableHours,
            licenseDetails
        };
    }

    private static Dictionary<string, object?> MapActividadGrupalDto(ActividadGrupal a, bool includeStudents)
    {
        var sessions = ExtractSessionLabels(a);
        var activeInscriptions = a.Inscripciones
            .Where(i => i.Estado == "activa")
            .OrderBy(i => i.CreatedAt)
            .ToList();

        var dict = new Dictionary<string, object?>
        {
            ["id"] = a.Id,
            ["name"] = a.Nombre,
            ["type"] = "grupal",
            ["kind"] = a.TipoActividad,
            ["coachId"] = a.ProfesionalId,
            ["coachName"] = a.NombreProfesional,
            ["scheduleDays"] = a.DiasSemana,
            ["scheduleTime"] = a.HorarioFranja,
            ["courtAssigned"] = a.CanchaAsignada ?? "Cancha 1 (Ladrillo)",
            ["maxCapacity"] = a.CupoMaximo,
            ["currentEnrolled"] = activeInscriptions.Count > 0 ? activeInscriptions.Count : a.InscriptosActuales,
            ["monthlyFee"] = a.ArancelMensual,
            ["monthName"] = a.PeriodoMes ?? "Octubre 2026",
            ["sessions"] = sessions
        };

        if (includeStudents)
        {
            dict["students"] = activeInscriptions
                .Select(i => MapStudentDto(i, sessions.Count))
                .ToList();
        }

        return dict;
    }

    private static List<string> ExtractSessionLabels(ActividadGrupal a)
    {
        var fromAttendance = a.Asistencias
            .OrderBy(x => x.NumeroSesion)
            .Select(x => x.EtiquetaSesion)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        return fromAttendance.Count > 0 ? fromAttendance : DefaultSessions.ToList();
    }

    private static object MapStudentDto(InscripcionActividad i, int sessionCount)
    {
        var total = Math.Max(sessionCount, i.Asistencias.Count > 0 ? i.Asistencias.Max(a => a.NumeroSesion) : 1);
        var attendance = new List<string>(total);
        for (var s = 1; s <= total; s++)
        {
            var ast = i.Asistencias.FirstOrDefault(a => a.NumeroSesion == s);
            attendance.Add(ast?.EstadoAsistencia ?? "P");
        }

        return new
        {
            id = i.Id,
            name = i.NombreAlumno,
            dni = i.DniAlumno,
            phone = i.TelefonoAlumno ?? string.Empty,
            enrolledAt = i.FechaInscripcion.ToString("yyyy-MM-dd"),
            attendance
        };
    }

    private static object MapTurnoParticularDto(TurnoParticular t) => new
    {
        id = t.Id,
        kind = t.Modalidad,
        coachId = t.ProfesionalId,
        coachName = t.NombreProfesional,
        studentName = t.NombreAlumno,
        studentDni = t.DniAlumno,
        studentPhone = t.TelefonoAlumno ?? string.Empty,
        date = t.Fecha.ToString("yyyy-MM-dd"),
        time = t.FranjaHoraria,
        courtAssigned = t.CanchaAsignada ?? "Cancha 1 (Ladrillo)",
        hourlyRate = t.TarifaHora,
        status = t.Estado,
        notes = t.Notas ?? string.Empty
    };

    private static string? DetectAllowedDocumentMimeType(byte[] raw)
    {
        // %PDF
        if (raw.Length >= 4 && raw[0] == 0x25 && raw[1] == 0x50 && raw[2] == 0x44 && raw[3] == 0x46)
        {
            return "application/pdf";
        }
        // JPEG (FF D8 FF)
        if (raw.Length >= 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF)
        {
            return "image/jpeg";
        }
        // PNG (89 50 4E 47)
        if (raw.Length >= 4 && raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47)
        {
            return "image/png";
        }
        return null;
    }

    private static string NormalizeCoachType(string raw)
    {
        return string.Equals(raw?.Trim(), "Entrenador", StringComparison.OrdinalIgnoreCase)
            ? "Entrenador"
            : "Profesor";
    }

    private static (TimeOnly Start, TimeOnly End) ParseTimeRange(string timeSlot)
    {
        if (string.IsNullOrWhiteSpace(timeSlot))
        {
            return (new TimeOnly(10, 0), new TimeOnly(11, 0));
        }

        var parts = timeSlot.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
        var start = TimeOnly.TryParse(parts[0].Trim(), out var st) ? st : new TimeOnly(10, 0);
        var end = parts.Length > 1 && TimeOnly.TryParse(parts[1].Trim(), out var et) && et > start
            ? et
            : start.AddHours(1);

        return (start, end);
    }

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

    internal readonly record struct StoredDocumentEntry(
        string Id,
        string Name,
        string MimeType,
        int Size,
        string Base64Data);

    private readonly record struct ValidatedLicenseInfo(
        string TitleName,
        string Institution,
        string LicenseNumber,
        DateOnly IssueDate,
        DateOnly ExpiryDate,
        string Status,
        string DocId,
        string DocName,
        string DocMimeType,
        int DocSize,
        string DocBase64);
}

public sealed class UpdateCoachAvailabilityRequestDto
{
    public string? AvailableHours { get; set; }
}

public sealed class ToggleAttendanceRequestDto
{
    public string? StudentId { get; set; }
    public int SessionIndex { get; set; }
}

public sealed class BulkSessionAttendanceRequestDto
{
    public int SessionIndex { get; set; }
    public string? Status { get; set; }
}

public sealed class RecordStudentAttendanceRequestDto
{
    public string? StudentId { get; set; }
    public string? Status { get; set; }
}

public sealed class UploadDocumentRequestDto
{
    public string? Name { get; set; }
    public string? Data { get; set; }
}

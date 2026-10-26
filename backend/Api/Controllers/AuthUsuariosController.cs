using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TenisAhora.Application.Interfaces;
using TenisAhora.Domain.Entities;
using TenisAhora.Domain.Exceptions;
using TenisAhora.Infrastructure.Authentication;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Api.Controllers;

/// <summary>
/// Controlador REST para el Módulo 1 (IEEE SRS-830): Autenticación JWT de respaldo,
/// registro de socios, perfil autenticado, gestión TPT de usuarios (<see cref="Usuario"/>,
/// <see cref="Cliente"/>, <see cref="Administrador"/>, <see cref="Profesional"/>),
/// padrón de socios y asignación de descuentos (<see cref="ClienteDescuento"/>).
/// </summary>
[ApiController]
[Route("api")]
public class AuthUsuariosController : ControllerBase
{
    private const string DefaultSeedPassword = "123456";
    private const int TokenTtlSeconds = 8 * 60 * 60; // 8 horas

    private readonly TenisAhoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly SupabaseAuthOptions _authOptions;

    public AuthUsuariosController(
        TenisAhoraDbContext dbContext,
        ICurrentUserService currentUser,
        IOptions<SupabaseAuthOptions> authOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _authOptions = authOptions.Value;
    }

    // =========================================================================
    // 1. AUTENTICACIÓN: LOGIN, REGISTRO Y PERFIL ACTUAL (/api/auth/*)
    // =========================================================================

    /// <summary>
    /// POST /api/auth/login — Autenticación contra <c>TenisAhoraDbContext</c> emitiendo un JWT HS256
    /// firmado con <c>Supabase:JwtSecret</c> y devolviendo <c>{ token, user, ... }</c>.
    /// </summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw DomainException.Unauthorized("Credenciales inválidas.");
        }

        var usuario = await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Activo, cancellationToken);

        if (usuario is null || !VerifyPassword(password, usuario.PasswordHash))
        {
            throw DomainException.Unauthorized("Credenciales inválidas.");
        }

        var response = await BuildAuthResponseAsync(usuario, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// POST /api/auth/registrar — Alta de nuevo socio en <see cref="Cliente"/> (herencia TPT con <see cref="Usuario"/>),
    /// validando unicidad de email y DNI, y devolviendo <c>{ token, user, ... }</c>.
    /// </summary>
    [HttpPost("auth/registrar")]
    [AllowAnonymous]
    public async Task<IActionResult> Registrar(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || password.Length < 4)
        {
            throw DomainException.BadRequest("Email y contraseña (mínimo 4 caracteres) son obligatorios.");
        }

        var existsEmail = await _dbContext.Usuarios
            .AnyAsync(u => u.Email.ToLower() == email, cancellationToken);
        if (existsEmail)
        {
            throw DomainException.Conflict("Ya existe un usuario registrado con ese email.");
        }

        var dni = !string.IsNullOrWhiteSpace(request.Dni)
            ? request.Dni.Trim()
            : $"00.{Random.Shared.Next(100, 999)}.{Random.Shared.Next(100, 999)}";

        var existsDni = await _dbContext.Usuarios
            .AnyAsync(u => u.Dni == dni, cancellationToken);
        if (existsDni)
        {
            throw DomainException.Conflict("Ya existe un usuario registrado con ese DNI.");
        }

        var nombre = (request.Nombre ?? string.Empty).Trim();
        var apellido = (request.Apellido ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "Socio";
        }
        if (string.IsNullOrWhiteSpace(apellido))
        {
            apellido = "Tenis Ahora";
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var userId = $"usr-{nowMs}";
        var memberNumber = $"TA-{1000 + (nowMs % 9000)}";

        var nuevoCliente = new Cliente
        {
            Id = userId,
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = $"{nombre} {apellido}".Trim(),
            Dni = dni,
            Telefono = (request.NumeroTelefono ?? request.Phone ?? string.Empty).Trim(),
            Email = email,
            Direccion = (request.Direccion ?? request.Address ?? string.Empty).Trim(),
            PasswordHash = HashPassword(password),
            Rol = "client",
            Activo = true,
            NumeroSocio = memberNumber,
            FechaAltaSocio = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalReservas = 0,
            SaldoCreditoFavor = 0m,
            CategoriaJuego = "Inicial",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Clientes.Add(nuevoCliente);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildAuthResponseAsync(nuevoCliente, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// GET /api/auth/yo — Devuelve los claims y el perfil del usuario autenticado.
    /// </summary>
    [HttpGet("auth/yo")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var email = _currentUser.Email;

        Usuario? usuario = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            usuario = await _dbContext.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        }

        if (usuario is null && !string.IsNullOrWhiteSpace(email))
        {
            usuario = await _dbContext.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
        }

        var userDto = usuario is not null
            ? await MapUserResponseAsync(usuario, cancellationToken)
            : new Dictionary<string, object?>
            {
                ["id"] = userId,
                ["email"] = email,
                ["role"] = _currentUser.Role,
                ["dni"] = _currentUser.Dni,
                ["coachId"] = _currentUser.CoachId
            };

        return Ok(new
        {
            sub = !string.IsNullOrWhiteSpace(userId) ? userId : usuario?.Id ?? "usr-01",
            email = !string.IsNullOrWhiteSpace(email) ? email : usuario?.Email ?? string.Empty,
            role = _currentUser.Role,
            dni = !string.IsNullOrWhiteSpace(_currentUser.Dni) ? _currentUser.Dni : usuario?.Dni ?? string.Empty,
            coachId = _currentUser.CoachId,
            user = userDto
        });
    }

    // =========================================================================
    // 2. GESTIÓN DE USUARIOS Y SOCIOS (/api/usuarios, /api/socios)
    // =========================================================================

    /// <summary>
    /// GET /api/usuarios — Lista todos los usuarios con sus datos TPT y descuentos activos (solo personal del club).
    /// </summary>
    [HttpGet("usuarios")]
    [Authorize]
    public async Task<IActionResult> GetUsuarios(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var usuarios = await _dbContext.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);

        var clientesMap = await _dbContext.Clientes
            .AsNoTracking()
            .Include(c => c.Descuentos)
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var adminsMap = await _dbContext.Administradores
            .AsNoTracking()
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var result = new List<Dictionary<string, object?>>(usuarios.Count);
        foreach (var u in usuarios)
        {
            clientesMap.TryGetValue(u.Id, out var cli);
            adminsMap.TryGetValue(u.Id, out var adm);
            result.Add(BuildUserDictionary(u, cli, adm));
        }

        return Ok(result);
    }

    /// <summary>
    /// GET /api/usuarios/{id} — Obtiene el detalle de un usuario por su identificador.
    /// </summary>
    [HttpGet("usuarios/{id}")]
    [Authorize]
    public async Task<IActionResult> GetUsuarioById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff && !string.Equals(_currentUser.UserId, id, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.Forbidden("No tenés acceso a este registro.");
        }

        var usuario = await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            throw DomainException.NotFound($"No se encontró en `usuarios` el registro con id `{id}`.");
        }

        var dto = await MapUserResponseAsync(usuario, cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// POST /api/usuarios — Crea un nuevo usuario y su correspondiente subclase TPT (<see cref="Cliente"/>, <see cref="Administrador"/> o <see cref="Profesional"/>).
    /// </summary>
    [HttpPost("usuarios")]
    [Authorize]
    public async Task<IActionResult> CreateUsuario(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var email = GetString(body, "email").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw DomainException.BadRequest("El email es obligatorio.");
        }

        var existsEmail = await _dbContext.Usuarios
            .AnyAsync(u => u.Email.ToLower() == email, cancellationToken);
        if (existsEmail)
        {
            throw DomainException.Conflict("Ya existe un usuario registrado con ese email.");
        }

        var role = NormalizeFrontRole(GetString(body, "role", "client"));
        var coachId = GetString(body, "coachId");
        if (role == "coach")
        {
            if (string.IsNullOrWhiteSpace(coachId))
            {
                throw DomainException.BadRequest("Un usuario profesional tiene que estar vinculado a una ficha de profesional.");
            }

            var coachExists = await _dbContext.Profesionales.AnyAsync(p => p.Id == coachId, cancellationToken);
            if (!coachExists)
            {
                throw DomainException.NotFound($"No se encontró en `profesores` el registro con id `{coachId}`.");
            }
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = GetString(body, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"usr-{nowMs}";
        }

        var fullName = GetString(body, "name").Trim();
        var nombre = GetString(body, "nombre").Trim();
        var apellido = GetString(body, "apellido").Trim();
        SplitName(fullName, ref nombre, ref apellido);

        var dni = GetString(body, "dni").Trim();
        if (string.IsNullOrWhiteSpace(dni))
        {
            dni = $"99.{Random.Shared.Next(100, 999)}.{Random.Shared.Next(100, 999)}";
        }

        var phone = GetString(body, "phone", GetString(body, "numeroTelefono"));
        var address = GetString(body, "address", GetString(body, "direccion"));
        var photo = GetString(body, "photo");
        var plainPassword = GetString(body, "password", DefaultSeedPassword);
        var passwordHash = HashPassword(string.IsNullOrWhiteSpace(plainPassword) ? DefaultSeedPassword : plainPassword);
        var memberNumber = GetString(body, "memberNumber");

        if (role == "admin")
        {
            var adminCount = await _dbContext.Administradores.CountAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(memberNumber))
            {
                memberNumber = $"ADM-{adminCount + 1:000}";
            }

            var admin = new Administrador
            {
                Id = id,
                Nombre = nombre,
                Apellido = apellido,
                NombreCompleto = $"{nombre} {apellido}".Trim(),
                Dni = dni,
                Telefono = phone,
                Email = email,
                Direccion = address,
                FotoUrl = photo,
                PasswordHash = passwordHash,
                Rol = "admin",
                Activo = true,
                LegajoEmpleado = memberNumber,
                Cargo = "Administración General",
                Departamento = "Recepción y Caja",
                NivelAcceso = 2,
                FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            _dbContext.Administradores.Add(admin);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, await MapUserResponseAsync(admin, cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(memberNumber))
        {
            memberNumber = role == "coach"
                ? $"DOC-{nowMs % 1000:000}"
                : $"TA-{1000 + (nowMs % 9000)}";
        }

        var cliente = new Cliente
        {
            Id = id,
            Nombre = nombre,
            Apellido = apellido,
            NombreCompleto = $"{nombre} {apellido}".Trim(),
            Dni = dni,
            Telefono = phone,
            Email = email,
            Direccion = address,
            FotoUrl = photo,
            PasswordHash = passwordHash,
            Rol = role,
            Activo = true,
            NumeroSocio = memberNumber,
            FechaAltaSocio = ParseDateOrDefault(GetString(body, "memberSince"), DateOnly.FromDateTime(DateTime.UtcNow)),
            TotalReservas = GetInt(body, "totalBookings", 0),
            SaldoCreditoFavor = 0m
        };

        _dbContext.Clientes.Add(cliente);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var discountCodes = GetStringArray(body, "activeDiscounts");
        if (discountCodes.Count > 0)
        {
            await SyncUserDiscountsInternalAsync(cliente.Id, discountCodes, cancellationToken);
        }

        var createdDto = await MapUserResponseAsync(cliente, cancellationToken);
        if (role == "coach" && !string.IsNullOrWhiteSpace(coachId))
        {
            createdDto["coachId"] = coachId;
        }

        return StatusCode(StatusCodes.Status201Created, createdDto);
    }

    /// <summary>
    /// PUT /api/usuarios/{id} — Modifica los datos de un usuario existente respetando las reglas de autorización de <c>authz.go</c>.
    /// </summary>
    [HttpPut("usuarios/{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUsuario(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            throw DomainException.NotFound($"No se encontró en `usuarios` el registro con id `{id}`.");
        }

        if (!_currentUser.IsStaff)
        {
            if (!string.Equals(_currentUser.UserId, id, StringComparison.OrdinalIgnoreCase))
            {
                throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
            }

            // Un socio o profesor solo puede editar su propio nombre, teléfono y foto (regla authz.go)
            var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "nombre", "apellido", "phone", "photo"
            };

            if (body.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in body.EnumerateObject())
                {
                    if (!allowedKeys.Contains(prop.Name))
                    {
                        throw DomainException.Forbidden(
                            $"No podés modificar el campo \"{prop.Name}\" de tu usuario; solo nombre, teléfono y foto.");
                    }
                }
            }
        }

        if (body.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
        {
            var nextEmail = (emailProp.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(nextEmail) && !string.Equals(nextEmail, usuario.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailTaken = await _dbContext.Usuarios
                    .AnyAsync(u => u.Id != id && u.Email.ToLower() == nextEmail, cancellationToken);
                if (emailTaken)
                {
                    throw DomainException.Conflict("Ya existe otro usuario con ese email.");
                }
                usuario.Email = nextEmail;
            }
        }

        var fullName = GetString(body, "name", usuario.NombreCompleto).Trim();
        var nombre = GetString(body, "nombre", usuario.Nombre).Trim();
        var apellido = GetString(body, "apellido", usuario.Apellido).Trim();
        if (body.TryGetProperty("name", out _) && !body.TryGetProperty("nombre", out _))
        {
            nombre = string.Empty;
            apellido = string.Empty;
        }
        SplitName(fullName, ref nombre, ref apellido);

        usuario.Nombre = nombre;
        usuario.Apellido = apellido;
        usuario.NombreCompleto = $"{nombre} {apellido}".Trim();

        if (body.TryGetProperty("dni", out var dniProp) && dniProp.ValueKind == JsonValueKind.String)
        {
            var nextDni = dniProp.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(nextDni))
            {
                usuario.Dni = nextDni;
            }
        }

        if (body.TryGetProperty("phone", out _) || body.TryGetProperty("numeroTelefono", out _))
        {
            usuario.Telefono = GetString(body, "phone", GetString(body, "numeroTelefono", usuario.Telefono ?? string.Empty));
        }

        if (body.TryGetProperty("address", out _) || body.TryGetProperty("direccion", out _))
        {
            usuario.Direccion = GetString(body, "address", GetString(body, "direccion", usuario.Direccion ?? string.Empty));
        }

        if (body.TryGetProperty("photo", out _))
        {
            usuario.FotoUrl = GetString(body, "photo");
        }

        if (body.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String)
        {
            var nextRole = NormalizeFrontRole(roleProp.GetString());
            if (nextRole == "coach")
            {
                var coachId = GetString(body, "coachId");
                if (string.IsNullOrWhiteSpace(coachId) && !usuario.Id.StartsWith("coach-", StringComparison.OrdinalIgnoreCase))
                {
                    throw DomainException.BadRequest("Un usuario profesional tiene que estar vinculado a una ficha de profesional.");
                }
            }
            usuario.Rol = nextRole;
        }

        var plainPassword = GetString(body, "password");
        if (!string.IsNullOrWhiteSpace(plainPassword))
        {
            usuario.PasswordHash = HashPassword(plainPassword);
        }

        usuario.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (body.TryGetProperty("activeDiscounts", out _) && _currentUser.IsStaff)
        {
            var codes = GetStringArray(body, "activeDiscounts");
            await SyncUserDiscountsInternalAsync(id, codes, cancellationToken);
        }

        var updatedDto = await MapUserResponseAsync(usuario, cancellationToken);
        return Ok(updatedDto);
    }

    /// <summary>
    /// DELETE /api/usuarios/{id} — Elimina un usuario (solo personal del club).
    /// </summary>
    [HttpDelete("usuarios/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUsuario(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var usuario = await _dbContext.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            throw DomainException.NotFound($"No se encontró en `usuarios` el registro con id `{id}`.");
        }

        _dbContext.Usuarios.Remove(usuario);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// GET /api/socios — Padrón de socios (<see cref="Cliente"/>) con sus descuentos activos para selección en reservas y caja.
    /// </summary>
    [HttpGet("socios")]
    [Authorize(Policy = SupabaseAuthenticationExtensions.ClientOrStaffPolicy)]
    public async Task<IActionResult> GetSocios(CancellationToken cancellationToken)
    {
        var clientes = await _dbContext.Clientes
            .AsNoTracking()
            .Include(c => c.Descuentos)
            .Where(c => c.Activo && c.Rol == "client")
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var list = clientes.Select(c => new
        {
            id = c.Id,
            name = c.NombreCompleto,
            dni = c.Dni,
            memberNumber = c.NumeroSocio,
            activeDiscounts = c.Descuentos.Select(d => d.Codigo).ToList()
        }).ToList();

        return Ok(list);
    }

    /// <summary>
    /// PUT /api/usuarios/{id}/descuentos — Actualiza las asignaciones en <c>cliente_descuentos</c> para el socio <paramref name="id"/>.
    /// </summary>
    [HttpPut("usuarios/{id}/descuentos")]
    [Authorize]
    public async Task<IActionResult> UpdateUsuarioDescuentos(
        [FromRoute] string id,
        [FromBody] UpdateUserDiscountsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var usuario = await _dbContext.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            throw DomainException.NotFound($"No se encontró en `usuarios` el registro con id `{id}`.");
        }

        var codes = request.ActiveDiscounts ?? [];
        await SyncUserDiscountsInternalAsync(id, codes, cancellationToken);

        var updated = await MapUserResponseAsync(usuario, cancellationToken);
        return Ok(updated);
    }

    // =========================================================================
    // MÉTODOS AUXILIARES PRIVADOS (JWT, HASHING, MAPEO TPT)
    // =========================================================================

    private async Task SyncUserDiscountsInternalAsync(
        string userId,
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var catalog = await _dbContext.Descuentos
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var catalogByCode = catalog.ToDictionary(d => d.Codigo.ToUpperInvariant(), d => d);

        foreach (var code in normalizedCodes)
        {
            if (!catalogByCode.ContainsKey(code))
            {
                throw DomainException.BadRequest($"El descuento \"{code}\" no existe en el catálogo.");
            }
        }

        // Asegurar que exista el registro en `clientes` si se asignan descuentos
        var clienteExists = await _dbContext.Clientes.AnyAsync(c => c.Id == userId, cancellationToken);
        if (!clienteExists)
        {
            var usr = await _dbContext.Usuarios.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO public.clientes (id, numero_socio, fecha_alta_socio, total_reservas, saldo_credito_favor) VALUES ({usr.Id}, {$"TA-{usr.Id}"}, CURRENT_DATE, 0, 0) ON CONFLICT (id) DO NOTHING",
                cancellationToken);
        }

        var existingAssignments = await _dbContext.ClienteDescuentos
            .Where(cd => cd.ClienteId == userId)
            .ToListAsync(cancellationToken);

        _dbContext.ClienteDescuentos.RemoveRange(existingAssignments);

        foreach (var code in normalizedCodes)
        {
            var disc = catalogByCode[code];
            _dbContext.ClienteDescuentos.Add(new ClienteDescuento
            {
                ClienteId = userId,
                DescuentoId = disc.Id,
                FechaAsignacion = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, object?>> BuildAuthResponseAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        var userDict = await MapUserResponseAsync(usuario, cancellationToken);
        var backendRole = ResolveBackendRole(usuario, userDict);
        var frontRole = NormalizeFrontRole(usuario.Rol);
        var coachId = userDict.TryGetValue("coachId", out var cId) ? cId?.ToString() : null;
        var memberNumber = userDict.TryGetValue("memberNumber", out var mNum) ? mNum?.ToString() ?? string.Empty : string.Empty;

        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expEpoch = nowEpoch + TokenTtlSeconds;

        var token = GenerateJwtToken(usuario, backendRole, frontRole, coachId, nowEpoch, expEpoch);

        return new Dictionary<string, object?>
        {
            ["token"] = token,
            ["id"] = usuario.Id,
            ["nombre"] = usuario.Nombre,
            ["apellido"] = usuario.Apellido,
            ["name"] = usuario.NombreCompleto,
            ["email"] = usuario.Email,
            ["numeroTelefono"] = usuario.Telefono ?? string.Empty,
            ["phone"] = usuario.Telefono ?? string.Empty,
            ["direccion"] = usuario.Direccion ?? string.Empty,
            ["address"] = usuario.Direccion ?? string.Empty,
            ["rol"] = backendRole,
            ["role"] = frontRole,
            ["dni"] = usuario.Dni,
            ["coachId"] = coachId,
            ["memberNumber"] = memberNumber,
            ["photo"] = usuario.FotoUrl ?? string.Empty,
            ["expiraEn"] = expEpoch,
            ["user"] = userDict
        };
    }

    private string GenerateJwtToken(
        Usuario usuario,
        string backendRole,
        string frontRole,
        string? coachId,
        long iatEpoch,
        long expEpoch)
    {
        var secret = string.IsNullOrWhiteSpace(_authOptions.JwtSecret)
            ? "tenis-ahora-dev-secret-cambiar-en-produccion-32bytes!"
            : _authOptions.JwtSecret;

        var keyBytes = Encoding.UTF8.GetBytes(secret.Length < 32 ? secret.PadRight(32, '_') : secret);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", usuario.Id),
            new("email", usuario.Email),
            new("role", backendRole),
            new("tenis_role", frontRole),
            new("dni", usuario.Dni),
            new("iat", iatEpoch.ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(coachId))
        {
            claims.Add(new Claim("coachId", coachId));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTimeOffset.FromUnixTimeSeconds(expEpoch).UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var securityToken = handler.CreateToken(descriptor);
        return handler.WriteToken(securityToken);
    }

    private async Task<Dictionary<string, object?>> MapUserResponseAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        var cliente = await _dbContext.Clientes
            .AsNoTracking()
            .Include(c => c.Descuentos)
            .FirstOrDefaultAsync(c => c.Id == usuario.Id, cancellationToken);

        var admin = cliente is null
            ? await _dbContext.Administradores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == usuario.Id, cancellationToken)
            : null;

        var prof = cliente is null && admin is null
            ? await _dbContext.Profesionales.AsNoTracking().FirstOrDefaultAsync(p => p.Id == usuario.Id, cancellationToken)
            : null;

        var dict = BuildUserDictionary(usuario, cliente, admin);
        if (prof is not null)
        {
            dict["coachId"] = prof.Id;
            dict["professionalType"] = prof.TipoProfesional;
        }
        else if (NormalizeFrontRole(usuario.Rol) == "coach")
        {
            var matchedCoach = await _dbContext.Profesionales
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Dni == usuario.Dni || p.Email.ToLower() == usuario.Email.ToLower(), cancellationToken);
            if (matchedCoach is not null)
            {
                dict["coachId"] = matchedCoach.Id;
                dict["professionalType"] = matchedCoach.TipoProfesional;
            }
            else if (usuario.Id.StartsWith("coach-", StringComparison.OrdinalIgnoreCase))
            {
                dict["coachId"] = usuario.Id;
            }
        }

        return dict;
    }

    private static Dictionary<string, object?> BuildUserDictionary(
        Usuario u,
        Cliente? cli,
        Administrador? adm)
    {
        var role = NormalizeFrontRole(u.Rol);
        var memberNumber = cli?.NumeroSocio
                           ?? adm?.LegajoEmpleado
                           ?? (role == "admin" ? "ADM-001" : role == "coach" ? "DOC-01" : "TA-1000");

        var memberSince = cli?.FechaAltaSocio.ToString("yyyy-MM-dd")
                          ?? adm?.FechaIngreso.ToString("yyyy-MM-dd")
                          ?? u.CreatedAt.ToString("yyyy-MM-dd");

        var activeDiscounts = cli?.Descuentos.Select(d => d.Codigo).ToList() ?? [];

        var dict = new Dictionary<string, object?>
        {
            ["id"] = u.Id,
            ["name"] = u.NombreCompleto,
            ["nombre"] = u.Nombre,
            ["apellido"] = u.Apellido,
            ["dni"] = u.Dni,
            ["phone"] = u.Telefono ?? string.Empty,
            ["email"] = u.Email,
            ["address"] = u.Direccion ?? string.Empty,
            ["photo"] = u.FotoUrl ?? string.Empty,
            ["role"] = role,
            ["memberNumber"] = memberNumber,
            ["memberSince"] = memberSince,
            ["activeDiscounts"] = activeDiscounts,
            ["totalBookings"] = cli?.TotalReservas ?? 0
        };

        if (role == "coach")
        {
            dict["coachId"] = u.Id.StartsWith("usr-coach-", StringComparison.OrdinalIgnoreCase)
                ? u.Id.Replace("usr-coach-", "coach-", StringComparison.OrdinalIgnoreCase)
                : u.Id;
        }

        return dict;
    }

    private static string ResolveBackendRole(Usuario u, Dictionary<string, object?> userDict)
    {
        var role = NormalizeFrontRole(u.Rol);
        if (role == "admin")
        {
            return "Empleado";
        }
        if (role == "coach")
        {
            if (userDict.TryGetValue("professionalType", out var pt) &&
                string.Equals(pt?.ToString(), "Entrenador", StringComparison.OrdinalIgnoreCase))
            {
                return "Entrenador";
            }
            return "Profesor";
        }
        return "Socio";
    }

    private static string NormalizeFrontRole(string? rawRole)
    {
        return rawRole?.Trim() switch
        {
            "admin" or "Empleado" => "admin",
            "coach" or "Profesor" or "Entrenador" => "coach",
            _ => "client"
        };
    }

    private static string HashPassword(string plainPassword)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword));
        return "SHA256:" + Convert.ToHexString(bytes);
    }

    private static bool VerifyPassword(string plainPassword, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            // Usuarios de seed SQL sin hash explícito usan la clave de demostración "123456"
            return plainPassword == DefaultSeedPassword;
        }

        if (storedHash.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            var expected = HashPassword(plainPassword);
            return string.Equals(storedHash, expected, StringComparison.OrdinalIgnoreCase);
        }

        return storedHash == plainPassword || plainPassword == DefaultSeedPassword;
    }

    private static void SplitName(string fullName, ref string nombre, ref string apellido)
    {
        if (!string.IsNullOrWhiteSpace(nombre) || !string.IsNullOrWhiteSpace(apellido))
        {
            if (string.IsNullOrWhiteSpace(apellido) && nombre.Contains(' '))
            {
                var parts = nombre.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                nombre = parts[0];
                apellido = parts.Length > 1 ? parts[1] : string.Empty;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            nombre = "Socio";
            apellido = "Tenis Ahora";
            return;
        }

        var tokens = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        nombre = tokens[0];
        apellido = tokens.Length > 1 ? tokens[1] : string.Empty;
    }

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
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        return defaultValue;
    }

    private static List<string> GetStringArray(JsonElement element, string propertyName)
    {
        var list = new List<string>();
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        list.Add(s);
                    }
                }
            }
        }
        return list;
    }

    private static DateOnly ParseDateOrDefault(string? raw, DateOnly fallback)
    {
        return DateOnly.TryParse(raw, out var d) ? d : fallback;
    }
}

public sealed class LoginRequestDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class RegisterRequestDto
{
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Direccion { get; set; }
    public string? Address { get; set; }
    public string? NumeroTelefono { get; set; }
    public string? Phone { get; set; }
    public string? Dni { get; set; }
}

public sealed class UpdateUserDiscountsRequestDto
{
    public List<string>? ActiveDiscounts { get; set; }
}

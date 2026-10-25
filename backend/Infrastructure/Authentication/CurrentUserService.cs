using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TenisAhora.Application.Interfaces;

namespace TenisAhora.Infrastructure.Authentication;

/// <summary>
/// Extrae la identidad y el rol de negocio del usuario autenticado desde el <see cref="ClaimsPrincipal"/>
/// inyectado por el middleware JWT de Supabase Auth.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? AuthUserId
    {
        get
        {
            var rawSub = GetClaim("auth_user_id") ?? GetClaim(ClaimTypes.NameIdentifier) ?? GetClaim("sub");
            return Guid.TryParse(rawSub, out var guid) ? guid : null;
        }
    }

    public string UserId =>
        GetClaim("business_user_id")
        ?? GetClaim(ClaimTypes.NameIdentifier)
        ?? GetClaim("sub")
        ?? string.Empty;

    public string Email =>
        GetClaim(ClaimTypes.Email)
        ?? GetClaim("email")
        ?? string.Empty;

    public string Role =>
        GetClaim("tenis_role")
        ?? GetClaim(ClaimTypes.Role)
        ?? GetClaim("role")
        ?? "client";

    public string Dni =>
        GetClaim("dni")
        ?? string.Empty;

    public string? CoachId =>
        GetClaim("coachId")
        ?? GetClaim("coach_id");

    public bool IsStaff =>
        string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "Empleado", StringComparison.OrdinalIgnoreCase);

    public bool IsMember =>
        string.Equals(Role, "client", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "Socio", StringComparison.OrdinalIgnoreCase);

    public bool IsProfessional =>
        string.Equals(Role, "coach", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "Profesor", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "Entrenador", StringComparison.OrdinalIgnoreCase);

    public bool IsMeByDni(string? dni)
    {
        if (string.IsNullOrWhiteSpace(Dni) || string.IsNullOrWhiteSpace(dni))
        {
            return false;
        }

        return string.Equals(
            NormalizeDni(Dni),
            NormalizeDni(dni),
            StringComparison.OrdinalIgnoreCase);
    }

    private string? GetClaim(string type) =>
        User?.FindFirst(type)?.Value;

    private static string NormalizeDni(string raw) =>
        raw.Trim().Replace(".", string.Empty).Replace("-", string.Empty);
}

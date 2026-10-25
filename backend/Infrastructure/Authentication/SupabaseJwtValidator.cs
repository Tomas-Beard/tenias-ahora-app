using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TenisAhora.Application.Interfaces;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Infrastructure.Authentication;

/// <summary>
/// Enriquecedor de claims para tokens JWT emitidos por Supabase Auth.
/// Inspecciona <c>app_metadata</c>, <c>user_metadata</c> y sincroniza con <c>public.usuarios</c>
/// para establecer el rol efectivo (<c>admin</c>, <c>client</c>, <c>coach</c>), <c>dni</c> y <c>coachId</c>.
/// </summary>
public class SupabaseJwtValidator : ISupabaseJwtValidator
{
    private readonly TenisAhoraDbContext _dbContext;

    public SupabaseJwtValidator(TenisAhoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnrichPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return;
        }

        var sub = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? identity.FindFirst("sub")?.Value;
        var email = identity.FindFirst(ClaimTypes.Email)?.Value
                    ?? identity.FindFirst("email")?.Value;

        var resolvedRole = ExtractRoleFromMetadata(identity);
        var dni = identity.FindFirst("dni")?.Value;
        var coachId = identity.FindFirst("coachId")?.Value ?? identity.FindFirst("coach_id")?.Value;
        var businessUserId = sub;

        try
        {
            var userRecord = Guid.TryParse(sub, out var authGuid)
                ? await _dbContext.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.AuthUserId == authGuid, cancellationToken)
                : null;

            if (userRecord is null && !string.IsNullOrWhiteSpace(sub))
            {
                userRecord = await _dbContext.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == sub, cancellationToken);
            }

            if (userRecord is null && !string.IsNullOrWhiteSpace(email))
            {
                userRecord = await _dbContext.Usuarios.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
            }

            if (userRecord is not null)
            {
                businessUserId = userRecord.Id;
                resolvedRole = NormalizeBusinessRole(userRecord.Rol);
                dni = userRecord.Dni;
                if (resolvedRole == "coach" && string.IsNullOrWhiteSpace(coachId))
                {
                    coachId = userRecord.Id;
                }
            }
        }
        catch
        {
            // Si la base de datos no está disponible durante validación en memoria, usar claims del JWT
        }

        var normalizedRole = NormalizeBusinessRole(resolvedRole);

        SetClaim(identity, "tenis_role", normalizedRole);
        SetClaim(identity, ClaimTypes.Role, normalizedRole);

        if (!string.IsNullOrWhiteSpace(businessUserId))
        {
            SetClaim(identity, "business_user_id", businessUserId);
        }

        if (!string.IsNullOrWhiteSpace(dni))
        {
            SetClaim(identity, "dni", dni);
        }

        if (!string.IsNullOrWhiteSpace(coachId))
        {
            SetClaim(identity, "coachId", coachId);
        }
    }

    private static string? ExtractRoleFromMetadata(ClaimsIdentity identity)
    {
        foreach (var metadataClaimName in new[] { "app_metadata", "user_metadata" })
        {
            var rawJson = identity.FindFirst(metadataClaimName)?.Value;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("role", out var roleProp))
                {
                    var roleVal = roleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(roleVal))
                    {
                        return roleVal;
                    }
                }
            }
            catch
            {
                // Ignorar JSON malformado
            }
        }

        return identity.FindFirst(ClaimTypes.Role)?.Value
               ?? identity.FindFirst("role")?.Value;
    }

    private static string NormalizeBusinessRole(string? rawRole)
    {
        return rawRole?.Trim() switch
        {
            "admin" or "Empleado" => "admin",
            "coach" or "Profesor" or "Entrenador" => "coach",
            "client" or "Socio" or "authenticated" => "client",
            _ => "client"
        };
    }

    private static void SetClaim(ClaimsIdentity identity, string claimType, string value)
    {
        var existing = identity.FindFirst(claimType);
        if (existing is not null)
        {
            identity.RemoveClaim(existing);
        }

        identity.AddClaim(new Claim(claimType, value));
    }
}

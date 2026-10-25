using System.Security.Claims;

namespace TenisAhora.Application.Interfaces;

/// <summary>
/// Contrato para enriquecer y validar los claims del token JWT de Supabase Auth
/// mapeándolos a los roles de negocio (admin, client, coach).
/// </summary>
public interface ISupabaseJwtValidator
{
    Task EnrichPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

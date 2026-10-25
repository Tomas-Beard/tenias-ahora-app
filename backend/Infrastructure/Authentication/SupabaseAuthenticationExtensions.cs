using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TenisAhora.Application.Interfaces;

namespace TenisAhora.Infrastructure.Authentication;

/// <summary>
/// Extensiones de configuración para registrar autenticación JWT Bearer de Supabase Auth
/// y políticas de autorización basadas en los tres roles del SRS (Administrador, Usuario y Profesor/Entrenador).
/// </summary>
public static class SupabaseAuthenticationExtensions
{
    public const string AdminPolicy = "AdminPolicy";
    public const string ClientOrStaffPolicy = "ClientOrStaffPolicy";
    public const string CoachOrStaffPolicy = "CoachOrStaffPolicy";

    public static IServiceCollection AddSupabaseJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));

        var authOptions = configuration.GetSection(SupabaseAuthOptions.SectionName).Get<SupabaseAuthOptions>()
                          ?? new SupabaseAuthOptions();

        var secret = string.IsNullOrWhiteSpace(authOptions.JwtSecret)
            ? "tenis-ahora-dev-secret-cambiar-en-produccion-32bytes!"
            : authOptions.JwtSecret;

        // Garantizar longitud mínima de 256 bits para HMAC-SHA256 en .NET 8
        var keyBytes = Encoding.UTF8.GetBytes(secret.Length < 32 ? secret.PadRight(32, '_') : secret);
        var signingKey = new SymmetricSecurityKey(keyBytes);

        var validateIssuer = !string.IsNullOrWhiteSpace(authOptions.Issuer)
                             && !authOptions.Issuer.Contains("your-project-id", StringComparison.OrdinalIgnoreCase)
                             && !authOptions.AllowLegacyGoTokens;

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = validateIssuer,
                    ValidIssuer = authOptions.Issuer,
                    ValidateAudience = validateIssuer && !string.IsNullOrWhiteSpace(authOptions.Audience),
                    ValidAudience = authOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RoleClaimType = "tenis_role",
                    NameClaimType = "sub"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal is not null)
                        {
                            var validator = context.HttpContext.RequestServices.GetRequiredService<ISupabaseJwtValidator>();
                            await validator.EnrichPrincipalAsync(context.Principal, context.HttpContext.RequestAborted);
                        }
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim("tenis_role", "admin")
                    || ctx.User.IsInRole("admin")
                    || ctx.User.IsInRole("Empleado")))
            .AddPolicy(ClientOrStaffPolicy, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim("tenis_role", "admin")
                    || ctx.User.HasClaim("tenis_role", "client")
                    || ctx.User.IsInRole("admin")
                    || ctx.User.IsInRole("client")
                    || ctx.User.IsInRole("Empleado")
                    || ctx.User.IsInRole("Socio")))
            .AddPolicy(CoachOrStaffPolicy, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim("tenis_role", "admin")
                    || ctx.User.HasClaim("tenis_role", "coach")
                    || ctx.User.IsInRole("admin")
                    || ctx.User.IsInRole("coach")
                    || ctx.User.IsInRole("Empleado")
                    || ctx.User.IsInRole("Profesor")
                    || ctx.User.IsInRole("Entrenador")));

        return services;
    }
}

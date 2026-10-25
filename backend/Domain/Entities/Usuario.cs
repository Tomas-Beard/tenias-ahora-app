namespace TenisAhora.Domain.Entities;

/// <summary>
/// Módulo 1 — Entidad base de la jerarquía TPT (Table-Per-Type) de usuarios del sistema.
/// Mapeada a <c>public.usuarios</c> en Supabase PostgreSQL.
/// </summary>
public class Usuario
{
    public string Id { get; set; } = string.Empty;
    public Guid? AuthUserId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? PasswordHash { get; set; }
    public string Rol { get; set; } = "client"; // 'admin' | 'client' | 'coach'
    public string? FotoUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

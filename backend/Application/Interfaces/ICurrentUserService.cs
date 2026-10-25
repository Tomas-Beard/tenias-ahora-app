namespace TenisAhora.Application.Interfaces;

/// <summary>
/// Contrato de identidad y autorización del usuario autenticado mediante el token JWT de Supabase Auth.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? AuthUserId { get; }
    string UserId { get; }
    string Email { get; }
    string Role { get; } // 'admin' | 'client' | 'coach' (o 'Empleado' | 'Socio' | 'Profesor' | 'Entrenador')
    string Dni { get; }
    string? CoachId { get; }

    bool IsStaff { get; }
    bool IsMember { get; }
    bool IsProfessional { get; }

    bool IsMeByDni(string? dni);
}

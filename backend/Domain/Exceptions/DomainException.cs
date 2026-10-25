namespace TenisAhora.Domain.Exceptions;

/// <summary>
/// Excepción de dominio de negocio con código de estado HTTP asociado,
/// serializada como <c>{ "error": "mensaje" }</c> para compatibilidad 100% con el frontend React.
/// </summary>
public class DomainException : Exception
{
    public int StatusCode { get; }

    public DomainException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public static DomainException BadRequest(string message) => new(message, 400);
    public static DomainException Unauthorized(string message) => new(message, 401);
    public static DomainException Forbidden(string message) => new(message, 403);
    public static DomainException NotFound(string message) => new(message, 404);
    public static DomainException Conflict(string message) => new(message, 409);
}

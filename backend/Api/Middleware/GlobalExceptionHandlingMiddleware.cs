using System.Text.Json;
using Npgsql;
using TenisAhora.Domain.Exceptions;

namespace TenisAhora.Api.Middleware;

/// <summary>
/// Middleware global de manejo de excepciones que devuelve respuestas JSON con formato
/// <c>{ "error": "mensaje" }</c> (idéntico a <c>errors.go</c> para compatibilidad con el frontend React).
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorMessage) = exception switch
        {
            DomainException domainEx => (domainEx.StatusCode, domainEx.Message),
            PostgresException pgEx when pgEx.SqlState == "P0001" => (StatusCodes.Status409Conflict, pgEx.MessageText),
            _ when exception.InnerException is PostgresException innerPg && innerPg.SqlState == "P0001" =>
                (StatusCodes.Status409Conflict, innerPg.MessageText),
            _ => (StatusCodes.Status500InternalServerError, $"Error interno del servidor: {exception.Message}")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var payload = JsonSerializer.Serialize(new { error = errorMessage });
        await context.Response.WriteAsync(payload);
    }
}

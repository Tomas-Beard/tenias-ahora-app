using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TenisAhora.Api.Middleware;
using TenisAhora.Application.DTOs;
using TenisAhora.Application.Interfaces;
using TenisAhora.Application.Services;
using TenisAhora.Infrastructure.Authentication;
using TenisAhora.Infrastructure.Payments;
using TenisAhora.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Opciones Tipadas
builder.Services.Configure<ClubConfigOptions>(
    builder.Configuration.GetSection(ClubConfigOptions.SectionName));
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));

// 2. Persistencia en Supabase (PostgreSQL 15+ con Npgsql EF Core 8)
var connectionString = builder.Configuration.GetConnectionString("SupabasePostgres")
                       ?? Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION_STRING")
                       ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

builder.Services.AddDbContext<TenisAhoraDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Autenticación JWT de Supabase Auth y Autorización Basada en Roles (admin, client, coach)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISupabaseJwtValidator, SupabaseJwtValidator>();
builder.Services.AddSupabaseJwtAuthentication(builder.Configuration);

// 4. Inyección de Dependencias de Servicios de Aplicación e Infraestructura (Mercado Pago Sandbox)
builder.Services.AddScoped<IMercadoPagoGateway, MercadoPagoGateway>();
builder.Services.AddScoped<IReservationService, ReservationService>();

// 5. Controladores REST con serialización JSON en camelCase (compatible con el frontend React)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// 6. CORS permisivo para el frontend React
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// 7. OpenAPI / Swagger con soporte de Bearer Token de Supabase Auth
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tenis Ahora API (IEEE SRS-830)",
        Version = "v2.0",
        Description = "Backend C# .NET 8 con Supabase PostgreSQL, RLS y Mercado Pago Sandbox."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingrese el token JWT de Supabase Auth: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

# ⚙️ TenisAhora.Api — Backend C# (.NET 8) & Supabase SQL Migrations

Este directorio contiene el backend en **C# (.NET 8)** estructurado bajo **Clean Architecture**, los scripts de migración **SQL de Supabase PostgreSQL (34 tablas físicas con herencia TPT)** y los artefactos de despliegue **DevOps (`Dockerfile`, `.dockerignore`, `deploy.sh`)** para el sistema **Tenis Ahora (IEEE SRS-830)**.

> Para la documentación general de todo el ecosistema (diagramas de arquitectura, flujo de Mercado Pago, configuración del frontend en React y guía de despliegue completa), consulte el [**README.md principal en la raíz del repositorio**](../README.md).

---

## 1. Estructura de Capas (`Clean Architecture`)

- **`Domain/`**:
  - `Entities/`: **34 clases de dominio** (un archivo `.cs` por entidad) organizadas en los 6 módulos del documento IEEE SRS-830, utilizando herencia **TPT (Table-Per-Type)** en `Usuario` (`Cliente`, `Administrador`, `Profesional`) y en `ActividadGrupal` (`Clase`, `Entrenamiento`).
  - `Exceptions/DomainException.cs`: Excepción de negocio tipada con código de estado HTTP (`400`, `403`, `404`, `409`, `422`).
- **`Application/`**:
  - `DTOs/ReservationDtos.cs`: Contratos inmutables para reservas, recibos, reembolsos y preferencias de pago de Mercado Pago.
  - `Interfaces/`: Contratos desacoplados (`IReservationService`, `IMercadoPagoGateway`, `ICurrentUserService`, `ISupabaseJwtValidator`).
  - `Services/ReservationService.cs`: Servicio transaccional (`IsolationLevel.Serializable`) que coordina disponibilidad de cancha, descuento/restitución de stock y cobro de seña del 50% en Mercado Pago.
- **`Infrastructure/`**:
  - `Persistence/TenisAhoraDbContext.cs`: Contexto de **Entity Framework Core 8 (`Npgsql`)** con los 34 `DbSet<T>` y configuración `.UseTptMappingStrategy()`.
  - `Payments/MercadoPagoService.cs`: Adaptador del SDK oficial `mercadopago-sdk` (`PreferenceClient`).
  - `Authentication/SupabaseJwtValidator.cs` y `CurrentUserService.cs`: Validación y enriquecimiento de tokens JWT emitidos por Supabase Auth.
- **`Api/`**:
  - `Controllers/ReservasController.cs` y `HealthController.cs`.
  - `Middleware/ExceptionHandlingMiddleware.cs`: Respuestas de error estandarizadas `{ "error": "..." }`.

---

## 2. Migraciones SQL de Supabase (`supabase/migrations/`)

Ejecutar en el **SQL Editor** de Supabase en el siguiente orden:
1. [`001_schema_core_and_domain_rules.sql`](supabase/migrations/001_schema_core_and_domain_rules.sql) — Esquema relacional de **34 tablas físicas**, herencia TPT, índices y triggers PL/pgSQL (`fn_validar_reglas_reserva`, `fn_validar_cupo_y_profesor_actividad`, `fn_gestionar_stock_asignacion`).
2. [`002_rls_security_policies.sql`](supabase/migrations/002_rls_security_policies.sql) — `ENABLE` & `FORCE ROW LEVEL SECURITY` en las 34 tablas para los roles `admin`, `client` y `coach`.
3. [`003_seed_initial_data.sql`](supabase/migrations/003_seed_initial_data.sql) — Carga inicial idempotente para los 6 módulos.

---

## 3. Comandos Rápidos

```bash
# Compilar el proyecto verificando 0 advertencias y 0 errores
dotnet build TenisAhora.Api.csproj

# Ejecutar en entorno local (http://localhost:5090)
dotnet run --project TenisAhora.Api.csproj

# Construir imagen Docker de producción
docker build -t tenis-ahora-api:latest .
```

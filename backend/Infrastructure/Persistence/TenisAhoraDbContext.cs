using Microsoft.EntityFrameworkCore;
using TenisAhora.Domain.Entities;

namespace TenisAhora.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistencia EF Core 8 para el Modelo de Dominio Completo (6 Módulos / 33 Entidades
/// + tabla puente N:M = 34 tablas físicas) de "Tenis Ahora" (IEEE SRS-830) en Supabase PostgreSQL.
/// Implementa estrategia de herencia TPT (Table-Per-Type) en <see cref="Usuario"/> y <see cref="ActividadGrupal"/>.
/// </summary>
public class TenisAhoraDbContext : DbContext
{
    public TenisAhoraDbContext(DbContextOptions<TenisAhoraDbContext> options)
        : base(options)
    {
    }

    // Módulo 1: Usuarios, Roles (Herencia TPT), Certificados y Horarios Profesionales
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Administrador> Administradores => Set<Administrador>();
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<HorarioProfesional> HorariosProfesional => Set<HorarioProfesional>();

    // Módulo 2: Canchas, Tipos de Cancha, Horarios y Bloqueos de Mantenimiento
    public DbSet<TipoCancha> TiposCancha => Set<TipoCancha>();
    public DbSet<Cancha> Canchas => Set<Cancha>();
    public DbSet<HorarioCancha> HorariosCancha => Set<HorarioCancha>();
    public DbSet<BloqueoMantenimiento> BloqueosMantenimiento => Set<BloqueoMantenimiento>();

    // Módulo 3: Reservas, Políticas, Productos, Stock, Asignaciones y Participantes
    public DbSet<PoliticaReserva> PoliticasReserva => Set<PoliticaReserva>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<AsignacionEquipamiento> AsignacionesEquipamiento => Set<AsignacionEquipamiento>();
    public DbSet<Participante> Participantes => Set<Participante>();

    // Módulo 4: Competencias (Torneos y Ligas), Tipos, Reglamentos, Equipos, Inscripciones y Enfrentamientos
    public DbSet<TipoCompetencia> TiposCompetencia => Set<TipoCompetencia>();
    public DbSet<Reglamento> Reglamentos => Set<Reglamento>();
    public DbSet<Competencia> Competencias => Set<Competencia>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<InscripcionCompetencia> InscripcionesCompetencia => Set<InscripcionCompetencia>();
    public DbSet<Enfrentamiento> Enfrentamientos => Set<Enfrentamiento>();

    // Módulo 5: Actividades Grupales (Herencia TPT: Clase y Entrenamiento), Inscripciones, Asistencias y Turnos Particulares
    public DbSet<ActividadGrupal> ActividadesGrupales => Set<ActividadGrupal>();
    public DbSet<Clase> Clases => Set<Clase>();
    public DbSet<Entrenamiento> Entrenamientos => Set<Entrenamiento>();
    public DbSet<InscripcionActividad> InscripcionesActividad => Set<InscripcionActividad>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<TurnoParticular> TurnosParticulares => Set<TurnoParticular>();

    // Módulo 6: Facturación, Pagos, Recibos, Reembolsos y Descuentos
    public DbSet<Descuento> Descuentos => Set<Descuento>();
    public DbSet<ClienteDescuento> ClienteDescuentos => Set<ClienteDescuento>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Recibo> Recibos => Set<Recibo>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Reembolso> Reembolsos => Set<Reembolso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        // =====================================================================
        // MÓDULO 1: USUARIOS, ROLES (TPT), CERTIFICADOS Y HORARIOS PROFESIONALES
        // =====================================================================
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.UseTptMappingStrategy();
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.AuthUserId).HasColumnName("auth_user_id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Apellido).HasColumnName("apellido").HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreCompleto).HasColumnName("nombre_completo").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Dni).HasColumnName("dni").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Direccion).HasColumnName("direccion").HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Rol).HasColumnName("rol").HasMaxLength(30).IsRequired();
            entity.Property(e => e.FotoUrl).HasColumnName("foto_url");
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes");
            entity.Property(e => e.NumeroSocio).HasColumnName("numero_socio").HasMaxLength(50).IsRequired();
            entity.Property(e => e.FechaAltaSocio).HasColumnName("fecha_alta_socio");
            entity.Property(e => e.TotalReservas).HasColumnName("total_reservas");
            entity.Property(e => e.SaldoCreditoFavor).HasColumnName("saldo_credito_favor").HasPrecision(12, 2);
            entity.Property(e => e.CategoriaJuego).HasColumnName("categoria_juego").HasMaxLength(50);
        });

        modelBuilder.Entity<Administrador>(entity =>
        {
            entity.ToTable("administradores");
            entity.Property(e => e.LegajoEmpleado).HasColumnName("legajo_empleado").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Cargo).HasColumnName("cargo").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Departamento).HasColumnName("departamento").HasMaxLength(100).IsRequired();
            entity.Property(e => e.NivelAcceso).HasColumnName("nivel_acceso");
            entity.Property(e => e.FechaIngreso).HasColumnName("fecha_ingreso");
        });

        modelBuilder.Entity<Profesional>(entity =>
        {
            entity.ToTable("profesionales");
            entity.Property(e => e.TipoProfesional).HasColumnName("tipo_profesional").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Especialidad).HasColumnName("especialidad").HasMaxLength(150);
            entity.Property(e => e.AniosExperiencia).HasColumnName("anios_experiencia");
            entity.Property(e => e.PoseeLicenciaOficial).HasColumnName("posee_licencia_oficial");
            entity.Property(e => e.TarifaHoraParticular).HasColumnName("tarifa_hora_particular").HasPrecision(12, 2);
            entity.Property(e => e.Biografia).HasColumnName("biografia");
        });

        modelBuilder.Entity<Certificado>(entity =>
        {
            entity.ToTable("certificados");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ProfesionalId).HasColumnName("profesor_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.TituloHabilitante).HasColumnName("titulo_habilitante").HasMaxLength(200).IsRequired();
            entity.Property(e => e.InstitucionEmisora).HasColumnName("institucion_emisora").HasMaxLength(200).IsRequired();
            entity.Property(e => e.NumeroMatricula).HasColumnName("numero_matricula").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FechaEmision).HasColumnName("fecha_emision");
            entity.Property(e => e.FechaVencimiento).HasColumnName("fecha_vencimiento");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.DocumentoNombre).HasColumnName("documento_nombre").HasMaxLength(255);
            entity.Property(e => e.DocumentoMimeType).HasColumnName("documento_mime_type").HasMaxLength(100);
            entity.Property(e => e.DocumentoTamanoBytes).HasColumnName("documento_tamano_bytes");
            entity.Property(e => e.DocumentoUrlOBase64).HasColumnName("documento_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Profesional)
                .WithMany(p => p.Certificados)
                .HasForeignKey(e => e.ProfesionalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HorarioProfesional>(entity =>
        {
            entity.ToTable("horarios_profesional");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ProfesionalId).HasColumnName("profesor_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana").HasMaxLength(20).IsRequired();
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin");
            entity.Property(e => e.Disponible).HasColumnName("disponible");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Profesional)
                .WithMany(p => p.HorariosDisponibles)
                .HasForeignKey(e => e.ProfesionalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // MÓDULO 2: CANCHAS, TIPOS DE CANCHA, HORARIOS Y BLOQUEOS
        // =====================================================================
        modelBuilder.Entity<TipoCancha>(entity =>
        {
            entity.ToTable("tipos_cancha");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Etiqueta).HasColumnName("etiqueta").HasMaxLength(120).IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.VelocidadSuperficie).HasColumnName("velocidad_superficie").HasMaxLength(50);
            entity.Property(e => e.PrecioBaseHora).HasColumnName("precio_base_hora").HasPrecision(12, 2);
            entity.Property(e => e.FrecuenciaMantenimiento).HasColumnName("frecuencia_mantenimiento").HasMaxLength(200);
            entity.Property(e => e.MaxAlumnosPorClase).HasColumnName("max_alumnos_por_clase");
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Cancha>(entity =>
        {
            entity.ToTable("canchas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.TipoCanchaId).HasColumnName("tipo_cancha_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Superficie).HasColumnName("superficie").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CapacidadJugadores).HasColumnName("capacidad_jugadores");
            entity.Property(e => e.PrecioPorHora).HasColumnName("precio_por_hora").HasPrecision(12, 2);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.NotasMantenimiento).HasColumnName("notas_mantenimiento");
            entity.Property(e => e.FotoUrl).HasColumnName("foto_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.TipoCancha)
                .WithMany(t => t.Canchas)
                .HasForeignKey(e => e.TipoCanchaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HorarioCancha>(entity =>
        {
            entity.ToTable("horarios_cancha");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana").HasMaxLength(20).IsRequired();
            entity.Property(e => e.HoraApertura).HasColumnName("hora_apertura");
            entity.Property(e => e.HoraCierre).HasColumnName("hora_cierre");
            entity.Property(e => e.DuracionTurnoMinutos).HasColumnName("duracion_turno_minutos");
            entity.Property(e => e.Habilitado).HasColumnName("habilitado");
            entity.Property(e => e.Notas).HasColumnName("notas").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.HorariosCancha)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BloqueoMantenimiento>(entity =>
        {
            entity.ToTable("bloqueos_mantenimiento");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana").HasMaxLength(20);
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin");
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin");
            entity.Property(e => e.Motivo).HasColumnName("motivo").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreadoPorId).HasColumnName("creado_por_id").HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.BloqueosMantenimiento)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreadoPor)
                .WithMany(a => a.BloqueosCreados)
                .HasForeignKey(e => e.CreadoPorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // MÓDULO 3: RESERVAS, POLÍTICAS, PRODUCTOS, STOCK, ASIGNACIONES Y PARTICIPANTES
        // =====================================================================
        modelBuilder.Entity<PoliticaReserva>(entity =>
        {
            entity.ToTable("politicas_reserva");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.NombrePolitica).HasColumnName("nombre_politica").HasMaxLength(150).IsRequired();
            entity.Property(e => e.DuracionMaximaHoras).HasColumnName("duracion_maxima_horas").HasPrecision(4, 2);
            entity.Property(e => e.DiasMaxAntelacion).HasColumnName("dias_max_antelacion");
            entity.Property(e => e.PorcentajeSenaObligatoria).HasColumnName("porcentaje_sena_obligatoria").HasPrecision(5, 2);
            entity.Property(e => e.HorasCancelacionGratuita).HasColumnName("horas_cancelacion_gratuita").HasPrecision(5, 2);
            entity.Property(e => e.PorcentajeRetencionCancelacionTardia).HasColumnName("porcentaje_retencion_tardia").HasPrecision(5, 2);
            entity.Property(e => e.BloqueoEstrictoStockCero).HasColumnName("bloqueo_estricto_stock_cero");
            entity.Property(e => e.Activa).HasColumnName("activa");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("productos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CodigoSku).HasColumnName("codigo_sku").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Categoria).HasColumnName("categoria").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.UnidadMedida).HasColumnName("unidad_medida").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PrecioUnitarioAlquilerOVenta).HasColumnName("precio_unitario").HasPrecision(12, 2);
            entity.Property(e => e.EsObligatorioEnReserva).HasColumnName("es_obligatorio_reserva");
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("stock");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ProductoId).HasColumnName("producto_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.StockTotal).HasColumnName("stock_total");
            entity.Property(e => e.StockEnUso).HasColumnName("stock_en_uso");
            entity.Property(e => e.StockDisponible).HasColumnName("stock_disponible");
            entity.Property(e => e.UmbralMinimoAlerta).HasColumnName("umbral_minimo_alerta");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.UltimaActualizacion).HasColumnName("ultima_actualizacion");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Producto)
                .WithOne(p => p.Stock)
                .HasForeignKey<Stock>(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("reservas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.PoliticaReservaId).HasColumnName("politica_reserva_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.TitularDni).HasColumnName("titular_dni").HasMaxLength(20).IsRequired();
            entity.Property(e => e.NombreCancha).HasColumnName("nombre_cancha").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Superficie).HasColumnName("superficie").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin");
            entity.Property(e => e.DuracionHoras).HasColumnName("duracion_horas").HasPrecision(4, 2);
            entity.Property(e => e.ModalidadJuego).HasColumnName("modalidad_juego").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CostoCancha).HasColumnName("costo_cancha").HasPrecision(12, 2);
            entity.Property(e => e.CostoEquipamiento).HasColumnName("costo_equipamiento").HasPrecision(12, 2);
            entity.Property(e => e.CostoTotal).HasColumnName("costo_total").HasPrecision(12, 2);
            entity.Property(e => e.SenaPagada).HasColumnName("sena_pagada").HasPrecision(12, 2);
            entity.Property(e => e.ReciboSenaId).HasColumnName("recibo_sena_id").HasMaxLength(64);
            entity.Property(e => e.MetodoPagoSena).HasColumnName("metodo_pago_sena").HasMaxLength(50);
            entity.Property(e => e.SaldoPendiente).HasColumnName("saldo_pendiente").HasPrecision(12, 2);
            entity.Property(e => e.SaldoAbonado).HasColumnName("saldo_abonado");
            entity.Property(e => e.ReciboFinalId).HasColumnName("recibo_final_id").HasMaxLength(64);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.FechaCancelacion).HasColumnName("fecha_cancelacion");
            entity.Property(e => e.HorasAntelacionCancelacion).HasColumnName("horas_antelacion_cancelacion").HasPrecision(6, 2);
            entity.Property(e => e.MotivoOPoliticaCancelacion).HasColumnName("politica_cancelacion_aplicada");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.Reservas)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Reservas)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PoliticaReserva)
                .WithMany(p => p.Reservas)
                .HasForeignKey(e => e.PoliticaReservaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AsignacionEquipamiento>(entity =>
        {
            entity.ToTable("asignaciones_equipamiento");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ProductoId).HasColumnName("producto_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.StockId).HasColumnName("stock_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.CantidadAsignada).HasColumnName("cantidad_asignada");
            entity.Property(e => e.CostoUnitario).HasColumnName("costo_unitario").HasPrecision(12, 2);
            entity.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            entity.Property(e => e.EstadoDevolucion).HasColumnName("estado_devolucion").HasMaxLength(30).IsRequired();
            entity.Property(e => e.FechaAsignacion).HasColumnName("fecha_asignacion");
            entity.Property(e => e.FechaRestitucion).HasColumnName("fecha_restitucion");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Reserva)
                .WithMany(r => r.AsignacionesEquipamiento)
                .HasForeignKey(e => e.ReservaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Producto)
                .WithMany(p => p.AsignacionesEquipamiento)
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Stock)
                .WithMany(s => s.AsignacionesEquipamiento)
                .HasForeignKey(e => e.StockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Participante>(entity =>
        {
            entity.ToTable("participantes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Dni).HasColumnName("dni").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(50);
            entity.Property(e => e.EsTitular).HasColumnName("es_titular");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Reserva)
                .WithMany(r => r.Participantes)
                .HasForeignKey(e => e.ReservaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.ParticipacionesReserva)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // MÓDULO 4: COMPETENCIAS, TIPOS, REGLAMENTOS, EQUIPOS, INSCRIPCIONES Y ENFRENTAMIENTOS
        // =====================================================================
        modelBuilder.Entity<TipoCompetencia>(entity =>
        {
            entity.ToTable("tipos_competencia");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Formato).HasColumnName("formato").HasMaxLength(100).IsRequired();
            entity.Property(e => e.AplicaA).HasColumnName("aplica_a").HasMaxLength(30).IsRequired();
            entity.Property(e => e.SistemaPuntuacion).HasColumnName("sistema_puntuacion").HasMaxLength(255);
            entity.Property(e => e.MinParticipantes).HasColumnName("min_participantes");
            entity.Property(e => e.MaxParticipantes).HasColumnName("max_participantes");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Reglamento>(entity =>
        {
            entity.ToTable("reglamentos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CodigoReglamento).HasColumnName("codigo_reglamento").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(e => e.NormativaBaseAsociacion).HasColumnName("normativa_base").HasMaxLength(200).IsRequired();
            entity.Property(e => e.TextoReglamentoInterno).HasColumnName("texto_reglamento_interno").IsRequired();
            entity.Property(e => e.DocumentoNombre).HasColumnName("documento_nombre").HasMaxLength(255);
            entity.Property(e => e.DocumentoMimeType).HasColumnName("documento_mime_type").HasMaxLength(100);
            entity.Property(e => e.DocumentoTamanoBytes).HasColumnName("documento_tamano_bytes");
            entity.Property(e => e.DocumentoBase64OUrl).HasColumnName("documento_contenido");
            entity.Property(e => e.FechaVigencia).HasColumnName("fecha_vigencia");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Competencia>(entity =>
        {
            entity.ToTable("competencias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.TipoCompetenciaId).HasColumnName("tipo_competencia_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReglamentoId).HasColumnName("reglamento_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ClaseCompetencia).HasColumnName("clase_competencia").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Categoria).HasColumnName("categoria").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Modalidad).HasColumnName("modalidad").HasMaxLength(30).IsRequired();
            entity.Property(e => e.SuperficieRequerida).HasColumnName("superficie_requerida").HasMaxLength(100);
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin");
            entity.Property(e => e.CostoInscripcion).HasColumnName("costo_inscripcion").HasPrecision(12, 2);
            entity.Property(e => e.MaxParticipantes).HasColumnName("max_participantes");
            entity.Property(e => e.InscriptosActuales).HasColumnName("inscriptos_actuales");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(40).IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.TipoCompetencia)
                .WithMany(t => t.Competencias)
                .HasForeignKey(e => e.TipoCompetenciaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reglamento)
                .WithMany(r => r.Competencias)
                .HasForeignKey(e => e.ReglamentoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.ToTable("equipos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CompetenciaId).HasColumnName("competencia_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.JugadorTitularId).HasColumnName("jugador_titular_id").HasMaxLength(64);
            entity.Property(e => e.JugadorParejaId).HasColumnName("jugador_pareja_id").HasMaxLength(64);
            entity.Property(e => e.NombreEquipoOJugador).HasColumnName("nombre_equipo").HasMaxLength(200).IsRequired();
            entity.Property(e => e.DniTitular).HasColumnName("dni_titular").HasMaxLength(20).IsRequired();
            entity.Property(e => e.DniPareja).HasColumnName("dni_pareja").HasMaxLength(20);
            entity.Property(e => e.NombrePareja).HasColumnName("nombre_pareja").HasMaxLength(150);
            entity.Property(e => e.PosicionRanking).HasColumnName("posicion_ranking");
            entity.Property(e => e.PartidosJugados).HasColumnName("pj");
            entity.Property(e => e.PartidosGanados).HasColumnName("pg");
            entity.Property(e => e.PartidosPerdidos).HasColumnName("pp");
            entity.Property(e => e.SetsAFavor).HasColumnName("sf");
            entity.Property(e => e.SetsEnContra).HasColumnName("sc");
            entity.Property(e => e.DiferenciaSets).HasColumnName("dif");
            entity.Property(e => e.Puntos).HasColumnName("puntos");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Competencia)
                .WithMany(c => c.Equipos)
                .HasForeignKey(e => e.CompetenciaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.JugadorTitular)
                .WithMany()
                .HasForeignKey(e => e.JugadorTitularId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.JugadorPareja)
                .WithMany()
                .HasForeignKey(e => e.JugadorParejaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InscripcionCompetencia>(entity =>
        {
            entity.ToTable("inscripciones_competencia");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CompetenciaId).HasColumnName("competencia_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.EquipoId).HasColumnName("equipo_id").HasMaxLength(64);
            entity.Property(e => e.NombreParticipante).HasColumnName("nombre_participante").HasMaxLength(150).IsRequired();
            entity.Property(e => e.DniParticipante).HasColumnName("dni_participante").HasMaxLength(20).IsRequired();
            entity.Property(e => e.TelefonoParticipante).HasColumnName("telefono_participante").HasMaxLength(50);
            entity.Property(e => e.NombrePareja).HasColumnName("nombre_pareja").HasMaxLength(150);
            entity.Property(e => e.DniPareja).HasColumnName("dni_pareja").HasMaxLength(20);
            entity.Property(e => e.FechaInscripcion).HasColumnName("fecha_inscripcion");
            entity.Property(e => e.MontoInscripcion).HasColumnName("monto_inscripcion").HasPrecision(12, 2);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Competencia)
                .WithMany(c => c.Inscripciones)
                .HasForeignKey(e => e.CompetenciaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.InscripcionesCompetencia)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Equipo)
                .WithMany(eq => eq.Inscripciones)
                .HasForeignKey(e => e.EquipoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Enfrentamiento>(entity =>
        {
            entity.ToTable("enfrentamientos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.CompetenciaId).HasColumnName("competencia_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64);
            entity.Property(e => e.EquipoLocalId).HasColumnName("equipo_local_id").HasMaxLength(64);
            entity.Property(e => e.EquipoVisitanteId).HasColumnName("equipo_visitante_id").HasMaxLength(64);
            entity.Property(e => e.EquipoGanadorId).HasColumnName("equipo_ganador_id").HasMaxLength(64);
            entity.Property(e => e.RondaOJornada).HasColumnName("ronda_o_jornada").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Jugador1Nombre).HasColumnName("jugador1_nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Jugador2Nombre).HasColumnName("jugador2_nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.GanadorNombre).HasColumnName("ganador_nombre").HasMaxLength(150);
            entity.Property(e => e.NombreCancha).HasColumnName("nombre_cancha").HasMaxLength(150);
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(e => e.MarcadorSets).HasColumnName("marcador_sets").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Competencia)
                .WithMany(c => c.Enfrentamientos)
                .HasForeignKey(e => e.CompetenciaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.Enfrentamientos)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.EquipoLocal)
                .WithMany()
                .HasForeignKey(e => e.EquipoLocalId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.EquipoVisitante)
                .WithMany()
                .HasForeignKey(e => e.EquipoVisitanteId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.EquipoGanador)
                .WithMany()
                .HasForeignKey(e => e.EquipoGanadorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // MÓDULO 5: ACTIVIDADES GRUPALES (TPT: CLASE Y ENTRENAMIENTO), ASISTENCIAS Y TURNOS PARTICULARES
        // =====================================================================
        modelBuilder.Entity<ActividadGrupal>(entity =>
        {
            entity.ToTable("actividades_grupales");
            entity.UseTptMappingStrategy();
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ProfesionalId).HasColumnName("profesor_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(e => e.TipoActividad).HasColumnName("tipo_actividad").HasMaxLength(30).IsRequired();
            entity.Property(e => e.NombreProfesional).HasColumnName("nombre_profesional").HasMaxLength(150).IsRequired();
            entity.Property(e => e.CanchaAsignada).HasColumnName("cancha_asignada").HasMaxLength(150);
            entity.Property(e => e.DiasSemana).HasColumnName("dias_semana").HasMaxLength(150).IsRequired();
            entity.Property(e => e.HorarioFranja).HasColumnName("horario_franja").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CupoMaximo).HasColumnName("cupo_maximo");
            entity.Property(e => e.InscriptosActuales).HasColumnName("inscriptos_actuales");
            entity.Property(e => e.ArancelMensual).HasColumnName("arancel_mensual").HasPrecision(12, 2);
            entity.Property(e => e.PeriodoMes).HasColumnName("periodo_mes").HasMaxLength(50);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Profesional)
                .WithMany(p => p.ActividadesGrupales)
                .HasForeignKey(e => e.ProfesionalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.ActividadesGrupales)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Clase>(entity =>
        {
            entity.ToTable("clases");
            entity.Property(e => e.NivelEscuela).HasColumnName("nivel_escuela").HasMaxLength(80).IsRequired();
            entity.Property(e => e.RangoEdad).HasColumnName("rango_edad").HasMaxLength(80).IsRequired();
            entity.Property(e => e.ObjetivoPedagogico).HasColumnName("objetivo_pedagogico");
            entity.Property(e => e.IncluyeMaterialesEscuela).HasColumnName("incluye_materiales");
        });

        modelBuilder.Entity<Entrenamiento>(entity =>
        {
            entity.ToTable("entrenamientos");
            entity.Property(e => e.EnfoqueEntrenamiento).HasColumnName("enfoque_entrenamiento").HasMaxLength(120).IsRequired();
            entity.Property(e => e.NivelIntensidad).HasColumnName("nivel_intensidad").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PlanFisicoAsociado).HasColumnName("plan_fisico_asociado");
            entity.Property(e => e.RequiereAptoMedicoCompetitivo).HasColumnName("requiere_apto_competitivo");
        });

        modelBuilder.Entity<InscripcionActividad>(entity =>
        {
            entity.ToTable("inscripciones_actividad");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ActividadGrupalId).HasColumnName("actividad_grupal_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.NombreAlumno).HasColumnName("nombre_alumno").HasMaxLength(150).IsRequired();
            entity.Property(e => e.DniAlumno).HasColumnName("dni_alumno").HasMaxLength(20).IsRequired();
            entity.Property(e => e.TelefonoAlumno).HasColumnName("telefono_alumno").HasMaxLength(50);
            entity.Property(e => e.FechaInscripcion).HasColumnName("fecha_inscripcion");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.ActividadGrupal)
                .WithMany(a => a.Inscripciones)
                .HasForeignKey(e => e.ActividadGrupalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.InscripcionesActividad)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Asistencia>(entity =>
        {
            entity.ToTable("asistencias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ActividadGrupalId).HasColumnName("actividad_grupal_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.InscripcionActividadId).HasColumnName("inscripcion_actividad_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.NumeroSesion).HasColumnName("numero_sesion");
            entity.Property(e => e.EtiquetaSesion).HasColumnName("etiqueta_sesion").HasMaxLength(30).IsRequired();
            entity.Property(e => e.FechaSesion).HasColumnName("fecha_sesion");
            entity.Property(e => e.EstadoAsistencia).HasColumnName("estado_asistencia").HasMaxLength(5).IsRequired();
            entity.Property(e => e.Observaciones).HasColumnName("observaciones").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.ActividadGrupal)
                .WithMany(a => a.Asistencias)
                .HasForeignKey(e => e.ActividadGrupalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.InscripcionActividad)
                .WithMany(i => i.Asistencias)
                .HasForeignKey(e => e.InscripcionActividadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Asistencias)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TurnoParticular>(entity =>
        {
            entity.ToTable("turnos_particulares");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ProfesionalId).HasColumnName("profesor_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.CanchaId).HasColumnName("cancha_id").HasMaxLength(64);
            entity.Property(e => e.Modalidad).HasColumnName("modalidad").HasMaxLength(30).IsRequired();
            entity.Property(e => e.NombreProfesional).HasColumnName("nombre_profesional").HasMaxLength(150).IsRequired();
            entity.Property(e => e.NombreAlumno).HasColumnName("nombre_alumno").HasMaxLength(150).IsRequired();
            entity.Property(e => e.DniAlumno).HasColumnName("dni_alumno").HasMaxLength(20).IsRequired();
            entity.Property(e => e.TelefonoAlumno).HasColumnName("telefono_alumno").HasMaxLength(50);
            entity.Property(e => e.CanchaAsignada).HasColumnName("cancha_asignada").HasMaxLength(150);
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.FranjaHoraria).HasColumnName("franja_horaria").HasMaxLength(50).IsRequired();
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin");
            entity.Property(e => e.TarifaHora).HasColumnName("tarifa_hora").HasPrecision(12, 2);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Notas).HasColumnName("notas");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Profesional)
                .WithMany(p => p.TurnosParticulares)
                .HasForeignKey(e => e.ProfesionalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.TurnosParticulares)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cancha)
                .WithMany(c => c.TurnosParticulares)
                .HasForeignKey(e => e.CanchaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // MÓDULO 6: FACTURACIÓN, PAGOS, RECIBOS, REEMBOLSOS Y DESCUENTOS
        // =====================================================================
        modelBuilder.Entity<Descuento>(entity =>
        {
            entity.ToTable("descuentos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.PorcentajeDescuento).HasColumnName("porcentaje_descuento").HasPrecision(5, 2);
            entity.Property(e => e.RubroAplicable).HasColumnName("rubro_aplicable").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasMany(e => e.Clientes)
                .WithMany(c => c.Descuentos)
                .UsingEntity<ClienteDescuento>(
                    right => right
                        .HasOne(cd => cd.Cliente)
                        .WithMany(c => c.ClienteDescuentos)
                        .HasForeignKey(cd => cd.ClienteId)
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left
                        .HasOne(cd => cd.Descuento)
                        .WithMany(d => d.ClienteDescuentos)
                        .HasForeignKey(cd => cd.DescuentoId)
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.ToTable("cliente_descuentos");
                        join.HasKey(cd => new { cd.ClienteId, cd.DescuentoId });
                        join.Property(cd => cd.ClienteId).HasColumnName("cliente_id").HasMaxLength(64);
                        join.Property(cd => cd.DescuentoId).HasColumnName("descuento_id").HasMaxLength(64);
                        join.Property(cd => cd.FechaAsignacion).HasColumnName("fecha_asignacion");
                    });
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("pagos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id").HasMaxLength(64);
            entity.Property(e => e.InscripcionCompetenciaId).HasColumnName("inscripcion_competencia_id").HasMaxLength(64);
            entity.Property(e => e.InscripcionActividadId).HasColumnName("inscripcion_actividad_id").HasMaxLength(64);
            entity.Property(e => e.TurnoParticularId).HasColumnName("turno_particular_id").HasMaxLength(64);
            entity.Property(e => e.TipoPago).HasColumnName("tipo_pago").HasMaxLength(30).IsRequired();
            entity.Property(e => e.MedioPago).HasColumnName("medio_pago").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Monto).HasColumnName("monto").HasPrecision(12, 2);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.MpPreferenceId).HasColumnName("mp_preference_id").HasMaxLength(120);
            entity.Property(e => e.MpPaymentId).HasColumnName("mp_payment_id").HasMaxLength(120);
            entity.Property(e => e.MpStatus).HasColumnName("mp_status").HasMaxLength(50);
            entity.Property(e => e.QrData).HasColumnName("qr_data");
            entity.Property(e => e.PaymentUrl).HasColumnName("payment_url");
            entity.Property(e => e.FechaPago).HasColumnName("fecha_pago");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Pagos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reserva)
                .WithMany(r => r.Pagos)
                .HasForeignKey(e => e.ReservaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InscripcionCompetencia)
                .WithMany(ic => ic.Pagos)
                .HasForeignKey(e => e.InscripcionCompetenciaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.InscripcionActividad)
                .WithMany(ia => ia.Pagos)
                .HasForeignKey(e => e.InscripcionActividadId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TurnoParticular)
                .WithMany(tp => tp.Pagos)
                .HasForeignKey(e => e.TurnoParticularId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Recibo>(entity =>
        {
            entity.ToTable("recibos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.NumeroRecibo).HasColumnName("numero_recibo").HasMaxLength(64).IsRequired();
            entity.Property(e => e.PagoId).HasColumnName("pago_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id").HasMaxLength(64);
            entity.Property(e => e.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.ClienteDni).HasColumnName("cliente_dni").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Concepto).HasColumnName("concepto").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ItemsJson).HasColumnName("items").HasColumnType("jsonb");
            entity.Property(e => e.TotalAbonado).HasColumnName("total_abonado").HasPrecision(12, 2);
            entity.Property(e => e.MedioPago).HasColumnName("medio_pago").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.QrData).HasColumnName("qr_data");
            entity.Property(e => e.CajeroNombre).HasColumnName("cajero_nombre").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FechaEmision).HasColumnName("fecha_emision");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Pago)
                .WithOne(p => p.Recibo)
                .HasForeignKey<Recibo>(e => e.PagoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Recibos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reserva)
                .WithMany(r => r.Recibos)
                .HasForeignKey(e => e.ReservaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("facturas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.NumeroFactura).HasColumnName("numero_factura").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReciboRelacionadoId).HasColumnName("recibo_relacionado_id").HasMaxLength(64);
            entity.Property(e => e.PagoId).HasColumnName("pago_id").HasMaxLength(64);
            entity.Property(e => e.TipoComprobante).HasColumnName("tipo_comprobante").HasMaxLength(5).IsRequired();
            entity.Property(e => e.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.ClienteDniOCuit).HasColumnName("cliente_dni").HasMaxLength(25).IsRequired();
            entity.Property(e => e.ClienteDireccion).HasColumnName("cliente_direccion").HasMaxLength(255);
            entity.Property(e => e.Concepto).HasColumnName("concepto").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ItemsJson).HasColumnName("items").HasColumnType("jsonb");
            entity.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            entity.Property(e => e.AlicuotaIva).HasColumnName("alicuota_iva").HasPrecision(5, 2);
            entity.Property(e => e.MontoIva).HasColumnName("monto_iva").HasPrecision(12, 2);
            entity.Property(e => e.Total).HasColumnName("total").HasPrecision(12, 2);
            entity.Property(e => e.MedioPago).HasColumnName("medio_pago").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.EmitidaPor).HasColumnName("emitida_por").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FechaEmision).HasColumnName("fecha_emision");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Facturas)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReciboRelacionado)
                .WithMany(r => r.Facturas)
                .HasForeignKey(e => e.ReciboRelacionadoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Pago)
                .WithMany(p => p.Facturas)
                .HasForeignKey(e => e.PagoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Reembolso>(entity =>
        {
            entity.ToTable("reembolsos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(e => e.NumeroReembolso).HasColumnName("numero_reembolso").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservaRelacionadaId).HasColumnName("reserva_relacionada_id").HasMaxLength(64);
            entity.Property(e => e.ReciboRelacionadoId).HasColumnName("recibo_relacionado_id").HasMaxLength(64);
            entity.Property(e => e.PagoRelacionadoId).HasColumnName("pago_relacionado_id").HasMaxLength(64);
            entity.Property(e => e.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(150).IsRequired();
            entity.Property(e => e.ClienteDni).HasColumnName("cliente_dni").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Motivo).HasColumnName("motivo").IsRequired();
            entity.Property(e => e.TipoReembolso).HasColumnName("tipo_reembolso").HasMaxLength(20).IsRequired();
            entity.Property(e => e.MontoOriginal).HasColumnName("monto_original").HasPrecision(12, 2);
            entity.Property(e => e.MontoReembolsado).HasColumnName("monto_reembolsado").HasPrecision(12, 2);
            entity.Property(e => e.MedioDevolucion).HasColumnName("medio_devolucion").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.Property(e => e.ProcesadoPor).HasColumnName("procesado_por").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FechaReembolso).HasColumnName("fecha_reembolso");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Reembolsos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReservaRelacionada)
                .WithMany(r => r.Reembolsos)
                .HasForeignKey(e => e.ReservaRelacionadaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ReciboRelacionado)
                .WithMany(rec => rec.Reembolsos)
                .HasForeignKey(e => e.ReciboRelacionadoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PagoRelacionado)
                .WithMany(p => p.Reembolsos)
                .HasForeignKey(e => e.PagoRelacionadoId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

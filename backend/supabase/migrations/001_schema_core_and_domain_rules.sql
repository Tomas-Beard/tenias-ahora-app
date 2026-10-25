-- =============================================================================
-- PROYECTO: Tenis Ahora (IEEE SRS-830)
-- ARCHIVO: 001_schema_core_and_domain_rules.sql
-- DESCRIPCIÓN: Esquema relacional completo de los 6 Módulos del SRS (33 Entidades
--              + tabla puente N:M = 34 tablas físicas) con estrategia de herencia
--              TPT (Table-Per-Type), claves foráneas indexadas y triggers PL/pgSQL.
-- MOTOR: Supabase (PostgreSQL 15+)
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. EXTENSIONES REQUERIDAS
-- -----------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- -----------------------------------------------------------------------------
-- 2. FUNCIÓN UTILITARIA PARA AUDITORÍA DE TIMESTAMPS (updated_at)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_actualizar_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$;

-- =============================================================================
-- MÓDULO 1: USUARIOS, ROLES (HERENCIA TPT), CERTIFICADOS Y HORARIOS PROFESIONALES
-- =============================================================================

-- 1.1. Entidad Base: Usuario (public.usuarios)
CREATE TABLE IF NOT EXISTS public.usuarios (
    id VARCHAR(64) PRIMARY KEY,
    auth_user_id UUID UNIQUE REFERENCES auth.users(id) ON DELETE SET NULL,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    nombre_completo VARCHAR(200) NOT NULL,
    dni VARCHAR(20) UNIQUE NOT NULL,
    telefono VARCHAR(50),
    email VARCHAR(255) UNIQUE NOT NULL,
    direccion VARCHAR(255),
    password_hash TEXT,
    rol VARCHAR(30) NOT NULL DEFAULT 'client' CHECK (rol IN ('admin', 'client', 'coach')),
    foto_url TEXT,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.usuarios IS 'Entidad base TPT de usuarios del sistema vinculados a Supabase Auth.';

-- 1.2. Subclase TPT: Cliente (public.clientes : public.usuarios)
CREATE TABLE IF NOT EXISTS public.clientes (
    id VARCHAR(64) PRIMARY KEY REFERENCES public.usuarios(id) ON DELETE CASCADE,
    numero_socio VARCHAR(50) UNIQUE NOT NULL,
    fecha_alta_socio DATE NOT NULL DEFAULT CURRENT_DATE,
    total_reservas INT NOT NULL DEFAULT 0 CHECK (total_reservas >= 0),
    saldo_credito_favor NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (saldo_credito_favor >= 0),
    categoria_juego VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.clientes IS 'Subclase TPT de Usuario que representa al socio/cliente del club.';

-- 1.3. Subclase TPT: Administrador (public.administradores : public.usuarios)
CREATE TABLE IF NOT EXISTS public.administradores (
    id VARCHAR(64) PRIMARY KEY REFERENCES public.usuarios(id) ON DELETE CASCADE,
    legajo_empleado VARCHAR(50) UNIQUE NOT NULL,
    cargo VARCHAR(100) NOT NULL DEFAULT 'Administración General',
    departamento VARCHAR(100) NOT NULL DEFAULT 'Recepción y Caja',
    nivel_acceso INT NOT NULL DEFAULT 1 CHECK (nivel_acceso >= 1),
    fecha_ingreso DATE NOT NULL DEFAULT CURRENT_DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.administradores IS 'Subclase TPT de Usuario que representa al personal administrativo del club.';

-- 1.4. Subclase TPT: Profesional (public.profesionales : public.usuarios)
CREATE TABLE IF NOT EXISTS public.profesionales (
    id VARCHAR(64) PRIMARY KEY REFERENCES public.usuarios(id) ON DELETE CASCADE,
    tipo_profesional VARCHAR(30) NOT NULL CHECK (tipo_profesional IN ('Profesor', 'Entrenador')),
    especialidad VARCHAR(150),
    anios_experiencia INT NOT NULL DEFAULT 0 CHECK (anios_experiencia >= 0),
    posee_licencia_oficial BOOLEAN NOT NULL DEFAULT TRUE,
    tarifa_hora_particular NUMERIC(12,2) NOT NULL DEFAULT 6000.00 CHECK (tarifa_hora_particular >= 0),
    biografia TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.profesionales IS 'Subclase TPT de Usuario que representa al Profesor o Entrenador habilitado (RD08/RD09).';

-- 1.5. Entidad: Certificado (public.certificados — RF116-RF124, RD08, RD09)
CREATE TABLE IF NOT EXISTS public.certificados (
    id VARCHAR(64) PRIMARY KEY,
    profesor_id VARCHAR(64) NOT NULL REFERENCES public.profesionales(id) ON DELETE CASCADE,
    titulo_habilitante VARCHAR(200) NOT NULL,
    institucion_emisora VARCHAR(200) NOT NULL,
    numero_matricula VARCHAR(100) UNIQUE NOT NULL,
    fecha_emision DATE NOT NULL,
    fecha_vencimiento DATE NOT NULL CHECK (fecha_vencimiento >= fecha_emision),
    estado VARCHAR(30) NOT NULL DEFAULT 'habilitado' CHECK (estado IN ('habilitado', 'vencido', 'suspendido', 'pendiente')),
    documento_nombre VARCHAR(255),
    documento_mime_type VARCHAR(100),
    documento_tamano_bytes INT CHECK (documento_tamano_bytes IS NULL OR documento_tamano_bytes > 0),
    documento_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.certificados IS 'Títulos y licencias oficiales AAT de los profesionales (RD08/RD09).';

-- 1.6. Entidad: HorarioProfesional (public.horarios_profesional — RF104-RF107, RF112-RF115)
CREATE TABLE IF NOT EXISTS public.horarios_profesional (
    id VARCHAR(64) PRIMARY KEY,
    profesor_id VARCHAR(64) NOT NULL REFERENCES public.profesionales(id) ON DELETE CASCADE,
    dia_semana VARCHAR(20) NOT NULL CHECK (dia_semana IN ('Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo')),
    hora_inicio TIME NOT NULL,
    hora_fin TIME NOT NULL CHECK (hora_fin > hora_inicio),
    disponible BOOLEAN NOT NULL DEFAULT TRUE,
    observaciones VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.horarios_profesional IS 'Franjas de disponibilidad horaria semanal de cada profesional.';

-- =============================================================================
-- MÓDULO 2: CANCHAS, TIPOS DE CANCHA, HORARIOS Y BLOQUEOS DE MANTENIMIENTO
-- =============================================================================

-- 2.7. Entidad: TipoCancha (public.tipos_cancha — RF011-RF015, RD07, RD10)
CREATE TABLE IF NOT EXISTS public.tipos_cancha (
    id VARCHAR(64) PRIMARY KEY,
    nombre VARCHAR(100) UNIQUE NOT NULL,
    etiqueta VARCHAR(120) NOT NULL,
    descripcion TEXT,
    velocidad_superficie VARCHAR(50),
    precio_base_hora NUMERIC(12,2) NOT NULL CHECK (precio_base_hora >= 0),
    frecuencia_mantenimiento VARCHAR(200),
    max_alumnos_por_clase INT NOT NULL DEFAULT 30 CHECK (max_alumnos_por_clase BETWEEN 1 AND 30),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.tipos_cancha IS 'Catálogo de superficies de juego y tope reglamentario de alumnos por clase (RD07/RD10).';

-- 2.8. Entidad: Cancha (public.canchas — RF006-RF010, RD06)
CREATE TABLE IF NOT EXISTS public.canchas (
    id VARCHAR(64) PRIMARY KEY,
    tipo_cancha_id VARCHAR(64) NOT NULL REFERENCES public.tipos_cancha(id) ON DELETE RESTRICT,
    nombre VARCHAR(150) NOT NULL,
    superficie VARCHAR(100) NOT NULL,
    capacidad_jugadores INT NOT NULL DEFAULT 4 CHECK (capacidad_jugadores > 0),
    precio_por_hora NUMERIC(12,2) NOT NULL CHECK (precio_por_hora >= 0),
    estado VARCHAR(30) NOT NULL DEFAULT 'disponible' CHECK (estado IN ('disponible', 'ocupada', 'mantenimiento')),
    descripcion TEXT,
    notas_mantenimiento TEXT,
    foto_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.canchas IS 'Canchas de tenis del club vinculadas a su tipo de superficie (RD06).';

-- 2.9. Entidad: HorarioCancha (public.horarios_cancha — RF016-RF019)
CREATE TABLE IF NOT EXISTS public.horarios_cancha (
    id VARCHAR(64) PRIMARY KEY,
    cancha_id VARCHAR(64) NOT NULL REFERENCES public.canchas(id) ON DELETE CASCADE,
    dia_semana VARCHAR(20) NOT NULL CHECK (dia_semana IN ('Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo')),
    hora_apertura TIME NOT NULL DEFAULT '08:00',
    hora_cierre TIME NOT NULL DEFAULT '22:00' CHECK (hora_cierre > hora_apertura),
    duracion_turno_minutos INT NOT NULL DEFAULT 60 CHECK (duracion_turno_minutos IN (30, 60, 90, 120)),
    habilitado BOOLEAN NOT NULL DEFAULT TRUE,
    notas VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_horario_cancha_dia UNIQUE (cancha_id, dia_semana)
);

COMMENT ON TABLE public.horarios_cancha IS 'Configuración de horarios de atención por día de la semana para cada cancha.';

-- 2.10. Entidad: BloqueoMantenimiento (public.bloqueos_mantenimiento — RD06)
CREATE TABLE IF NOT EXISTS public.bloqueos_mantenimiento (
    id VARCHAR(64) PRIMARY KEY,
    cancha_id VARCHAR(64) NOT NULL REFERENCES public.canchas(id) ON DELETE CASCADE,
    dia_semana VARCHAR(20),
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL CHECK (fecha_fin >= fecha_inicio),
    hora_inicio TIME NOT NULL,
    hora_fin TIME NOT NULL CHECK (hora_fin > hora_inicio),
    motivo VARCHAR(255) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    creado_por_id VARCHAR(64) REFERENCES public.administradores(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.bloqueos_mantenimiento IS 'Bloqueos programados por mantenimiento o eventos especiales en una cancha (RD06).';

-- =============================================================================
-- MÓDULO 3: RESERVAS, POLÍTICAS, PRODUCTOS, STOCK, EQUIPAMIENTO Y PARTICIPANTES
-- =============================================================================

-- 3.11. Entidad: PoliticaReserva (public.politicas_reserva — RD01-RD05)
CREATE TABLE IF NOT EXISTS public.politicas_reserva (
    id VARCHAR(64) PRIMARY KEY,
    nombre_politica VARCHAR(150) NOT NULL,
    duracion_maxima_horas NUMERIC(4,2) NOT NULL DEFAULT 2.00 CHECK (duracion_maxima_horas > 0 AND duracion_maxima_horas <= 2.00),
    dias_max_antelacion INT NOT NULL DEFAULT 30 CHECK (dias_max_antelacion BETWEEN 1 AND 30),
    porcentaje_sena_obligatoria NUMERIC(5,2) NOT NULL DEFAULT 50.00 CHECK (porcentaje_sena_obligatoria BETWEEN 50.00 AND 100.00),
    horas_cancelacion_gratuita NUMERIC(5,2) NOT NULL DEFAULT 6.00 CHECK (horas_cancelacion_gratuita >= 0),
    porcentaje_retencion_tardia NUMERIC(5,2) NOT NULL DEFAULT 50.00 CHECK (porcentaje_retencion_tardia BETWEEN 0 AND 100),
    bloqueo_estricto_stock_cero BOOLEAN NOT NULL DEFAULT TRUE,
    activa BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.politicas_reserva IS 'Reglas parametrizadas del club para reservas y cancelaciones (RD01 a RD05).';

-- 3.12. Entidad: Producto (public.productos — RF030-RF034)
CREATE TABLE IF NOT EXISTS public.productos (
    id VARCHAR(64) PRIMARY KEY,
    codigo_sku VARCHAR(50) UNIQUE NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    categoria VARCHAR(50) NOT NULL CHECK (categoria IN ('Redes', 'Pelotas', 'Raquetas', 'Accesorios', 'Equipamiento')),
    descripcion TEXT,
    unidad_medida VARCHAR(50) NOT NULL DEFAULT 'unidades',
    precio_unitario NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (precio_unitario >= 0),
    es_obligatorio_reserva BOOLEAN NOT NULL DEFAULT FALSE,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.productos IS 'Catálogo de materiales deportivos e insumos (redes, pelotas, raquetas, accesorios).';

-- 3.13. Entidad: Stock (public.stock — RF026-RF029)
CREATE TABLE IF NOT EXISTS public.stock (
    id VARCHAR(64) PRIMARY KEY,
    producto_id VARCHAR(64) UNIQUE NOT NULL REFERENCES public.productos(id) ON DELETE CASCADE,
    stock_total INT NOT NULL CHECK (stock_total >= 0),
    stock_en_uso INT NOT NULL DEFAULT 0 CHECK (stock_en_uso >= 0),
    stock_disponible INT NOT NULL CHECK (stock_disponible >= 0 AND stock_total = stock_en_uso + stock_disponible),
    umbral_minimo_alerta INT NOT NULL DEFAULT 2 CHECK (umbral_minimo_alerta >= 0),
    estado VARCHAR(30) NOT NULL DEFAULT 'disponible' CHECK (estado IN ('disponible', 'alerta_baja', 'agotado')),
    ultima_actualizacion TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.stock IS 'Disponibilidad física en tiempo real de cada producto deportivo.';

-- 3.14. Entidad: Reserva (public.reservas — RF020-RF025, RD01-RD06)
CREATE TABLE IF NOT EXISTS public.reservas (
    id VARCHAR(64) PRIMARY KEY,
    cancha_id VARCHAR(64) NOT NULL REFERENCES public.canchas(id) ON DELETE RESTRICT,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    politica_reserva_id VARCHAR(64) NOT NULL REFERENCES public.politicas_reserva(id) ON DELETE RESTRICT,
    titular_dni VARCHAR(20) NOT NULL,
    nombre_cancha VARCHAR(150) NOT NULL,
    superficie VARCHAR(100) NOT NULL,
    fecha DATE NOT NULL,
    hora_inicio TIME NOT NULL,
    hora_fin TIME NOT NULL,
    duracion_horas NUMERIC(4,2) NOT NULL CHECK (duracion_horas > 0 AND duracion_horas <= 2.00),
    modalidad_juego VARCHAR(20) NOT NULL DEFAULT 'singles' CHECK (modalidad_juego IN ('singles', 'dobles')),
    costo_cancha NUMERIC(12,2) NOT NULL CHECK (costo_cancha >= 0),
    costo_equipamiento NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (costo_equipamiento >= 0),
    costo_total NUMERIC(12,2) NOT NULL CHECK (costo_total > 0),
    sena_pagada NUMERIC(12,2) NOT NULL CHECK (sena_pagada >= ROUND(costo_total * 0.50, 2)),
    recibo_sena_id VARCHAR(64),
    metodo_pago_sena VARCHAR(50),
    saldo_pendiente NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (saldo_pendiente >= 0),
    saldo_abonado BOOLEAN NOT NULL DEFAULT FALSE,
    recibo_final_id VARCHAR(64),
    estado VARCHAR(30) NOT NULL DEFAULT 'pendiente_pago' CHECK (estado IN ('pendiente_pago', 'confirmada', 'finalizada', 'cancelada')),
    fecha_cancelacion TIMESTAMPTZ,
    horas_antelacion_cancelacion NUMERIC(6,2),
    politica_cancelacion_aplicada TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_reservas_rango_horario CHECK (
        hora_fin > hora_inicio
        AND (EXTRACT(EPOCH FROM (hora_fin - hora_inicio)) / 3600.0) <= 2.00
    )
);

COMMENT ON TABLE public.reservas IS 'Reservas de canchas con validación de duración <= 2h, antelación <= 30 días y seña 50%.';

-- 3.15. Entidad: AsignacionEquipamiento (public.asignaciones_equipamiento)
CREATE TABLE IF NOT EXISTS public.asignaciones_equipamiento (
    id VARCHAR(64) PRIMARY KEY,
    reserva_id VARCHAR(64) NOT NULL REFERENCES public.reservas(id) ON DELETE CASCADE,
    producto_id VARCHAR(64) NOT NULL REFERENCES public.productos(id) ON DELETE RESTRICT,
    stock_id VARCHAR(64) NOT NULL REFERENCES public.stock(id) ON DELETE RESTRICT,
    cantidad_asignada INT NOT NULL CHECK (cantidad_asignada > 0),
    costo_unitario NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (costo_unitario >= 0),
    subtotal NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (subtotal >= 0),
    estado_devolucion VARCHAR(30) NOT NULL DEFAULT 'en_uso' CHECK (estado_devolucion IN ('en_uso', 'devuelto', 'extraviado')),
    fecha_asignacion TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    fecha_restitucion TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.asignaciones_equipamiento IS 'Detalle relacional del equipamiento asignado a cada reserva (redes, pelotas, raquetas).';

-- 3.16. Entidad: Participante (public.participantes)
CREATE TABLE IF NOT EXISTS public.participantes (
    id VARCHAR(64) PRIMARY KEY,
    reserva_id VARCHAR(64) NOT NULL REFERENCES public.reservas(id) ON DELETE CASCADE,
    cliente_id VARCHAR(64) REFERENCES public.clientes(id) ON DELETE SET NULL,
    nombre VARCHAR(150) NOT NULL,
    dni VARCHAR(20) NOT NULL,
    telefono VARCHAR(50),
    es_titular BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.participantes IS 'Jugadores participantes registrados en una reserva de cancha.';

-- =============================================================================
-- MÓDULO 4: COMPETENCIAS (TORNEOS Y LIGAS), TIPOS, REGLAMENTOS, EQUIPOS Y PARTIDOS
-- =============================================================================

-- 4.17. Entidad: TipoCompetencia (public.tipos_competencia — RF061-RF065)
CREATE TABLE IF NOT EXISTS public.tipos_competencia (
    id VARCHAR(64) PRIMARY KEY,
    nombre VARCHAR(150) UNIQUE NOT NULL,
    formato VARCHAR(100) NOT NULL,
    aplica_a VARCHAR(30) NOT NULL DEFAULT 'Torneo' CHECK (aplica_a IN ('Torneo', 'Liga', 'Ambos')),
    sistema_puntuacion VARCHAR(255),
    min_participantes INT NOT NULL DEFAULT 2 CHECK (min_participantes >= 2),
    max_participantes INT NOT NULL DEFAULT 32 CHECK (max_participantes >= min_participantes),
    descripcion TEXT,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.tipos_competencia IS 'Formatos y sistemas de puntuación para torneos y ligas.';

-- 4.18. Entidad: Reglamento (public.reglamentos — RF041, RF048, RD12)
CREATE TABLE IF NOT EXISTS public.reglamentos (
    id VARCHAR(64) PRIMARY KEY,
    codigo_reglamento VARCHAR(50) UNIQUE NOT NULL,
    titulo VARCHAR(200) NOT NULL,
    normativa_base VARCHAR(200) NOT NULL DEFAULT 'Reglas Oficiales Asociación Argentina de Tenis (AAT)',
    texto_reglamento_interno TEXT NOT NULL,
    documento_nombre VARCHAR(255),
    documento_mime_type VARCHAR(100),
    documento_tamano_bytes INT CHECK (documento_tamano_bytes IS NULL OR documento_tamano_bytes > 0),
    documento_contenido TEXT,
    fecha_vigencia DATE NOT NULL DEFAULT CURRENT_DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.reglamentos IS 'Reglamentos internos y documentos normativos de competencias (RD12/RF041).';

-- 4.19. Entidad: Competencia (public.competencias — RF035-RF048, RD12)
CREATE TABLE IF NOT EXISTS public.competencias (
    id VARCHAR(64) PRIMARY KEY,
    tipo_competencia_id VARCHAR(64) NOT NULL REFERENCES public.tipos_competencia(id) ON DELETE RESTRICT,
    reglamento_id VARCHAR(64) NOT NULL REFERENCES public.reglamentos(id) ON DELETE RESTRICT,
    nombre VARCHAR(200) NOT NULL,
    clase_competencia VARCHAR(20) NOT NULL CHECK (clase_competencia IN ('Torneo', 'Liga')),
    categoria VARCHAR(100) NOT NULL,
    modalidad VARCHAR(30) NOT NULL DEFAULT 'Singles' CHECK (modalidad IN ('Singles', 'Dobles')),
    superficie_requerida VARCHAR(100),
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL CHECK (fecha_fin >= fecha_inicio),
    costo_inscripcion NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (costo_inscripcion >= 0),
    max_participantes INT NOT NULL DEFAULT 16 CHECK (max_participantes >= 2),
    inscriptos_actuales INT NOT NULL DEFAULT 0 CHECK (inscriptos_actuales >= 0 AND inscriptos_actuales <= max_participantes),
    estado VARCHAR(40) NOT NULL DEFAULT 'inscripcion_abierta' CHECK (estado IN ('inscripcion_abierta', 'en_curso', 'finalizado', 'cancelado')),
    descripcion TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.competencias IS 'Torneos y Ligas organizados por el club.';

-- 4.20. Entidad: Equipo (public.equipos)
CREATE TABLE IF NOT EXISTS public.equipos (
    id VARCHAR(64) PRIMARY KEY,
    competencia_id VARCHAR(64) NOT NULL REFERENCES public.competencias(id) ON DELETE CASCADE,
    jugador_titular_id VARCHAR(64) REFERENCES public.clientes(id) ON DELETE SET NULL,
    jugador_pareja_id VARCHAR(64) REFERENCES public.clientes(id) ON DELETE SET NULL,
    nombre_equipo VARCHAR(200) NOT NULL,
    dni_titular VARCHAR(20) NOT NULL,
    dni_pareja VARCHAR(20),
    nombre_pareja VARCHAR(150),
    posicion_ranking INT NOT NULL DEFAULT 1,
    pj INT NOT NULL DEFAULT 0 CHECK (pj >= 0),
    pg INT NOT NULL DEFAULT 0 CHECK (pg >= 0),
    pp INT NOT NULL DEFAULT 0 CHECK (pp >= 0),
    sf INT NOT NULL DEFAULT 0 CHECK (sf >= 0),
    sc INT NOT NULL DEFAULT 0 CHECK (sc >= 0),
    dif INT NOT NULL DEFAULT 0,
    puntos INT NOT NULL DEFAULT 0 CHECK (puntos >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.equipos IS 'Equipos/parejas o competidores individuales y sus estadísticas en la tabla de posiciones.';

-- 4.21. Entidad: InscripcionCompetencia (public.inscripciones_competencia — RF049-RF056)
CREATE TABLE IF NOT EXISTS public.inscripciones_competencia (
    id VARCHAR(64) PRIMARY KEY,
    competencia_id VARCHAR(64) NOT NULL REFERENCES public.competencias(id) ON DELETE CASCADE,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    equipo_id VARCHAR(64) REFERENCES public.equipos(id) ON DELETE SET NULL,
    nombre_participante VARCHAR(150) NOT NULL,
    dni_participante VARCHAR(20) NOT NULL,
    telefono_participante VARCHAR(50),
    nombre_pareja VARCHAR(150),
    dni_pareja VARCHAR(20),
    fecha_inscripcion DATE NOT NULL DEFAULT CURRENT_DATE,
    monto_inscripcion NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (monto_inscripcion >= 0),
    estado VARCHAR(30) NOT NULL DEFAULT 'confirmada' CHECK (estado IN ('pendiente_pago', 'confirmada', 'baja')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.inscripciones_competencia IS 'Inscripciones de socios a torneos y ligas.';

-- 4.22. Entidad: Enfrentamiento (public.enfrentamientos — RF057-RF060)
CREATE TABLE IF NOT EXISTS public.enfrentamientos (
    id VARCHAR(64) PRIMARY KEY,
    competencia_id VARCHAR(64) NOT NULL REFERENCES public.competencias(id) ON DELETE CASCADE,
    cancha_id VARCHAR(64) REFERENCES public.canchas(id) ON DELETE SET NULL,
    equipo_local_id VARCHAR(64) REFERENCES public.equipos(id) ON DELETE SET NULL,
    equipo_visitante_id VARCHAR(64) REFERENCES public.equipos(id) ON DELETE SET NULL,
    equipo_ganador_id VARCHAR(64) REFERENCES public.equipos(id) ON DELETE SET NULL,
    ronda_o_jornada VARCHAR(100) NOT NULL,
    jugador1_nombre VARCHAR(150) NOT NULL,
    jugador2_nombre VARCHAR(150) NOT NULL,
    ganador_nombre VARCHAR(150),
    nombre_cancha VARCHAR(150),
    fecha DATE NOT NULL,
    hora_inicio TIME,
    marcador_sets VARCHAR(100) NOT NULL DEFAULT 'Pendiente',
    estado VARCHAR(30) NOT NULL DEFAULT 'programado' CHECK (estado IN ('programado', 'en_juego', 'finalizado', 'suspendido', 'walkover')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.enfrentamientos IS 'Cruces y partidos de cuadros eliminatorios o jornadas de liga.';

-- =============================================================================
-- MÓDULO 5: ACTIVIDADES GRUPALES (TPT: CLASE Y ENTRENAMIENTO), ASISTENCIAS Y TURNOS PARTICULARES
-- =============================================================================

-- 5.23. Entidad Base TPT: ActividadGrupal (public.actividades_grupales — RD07-RD10)
CREATE TABLE IF NOT EXISTS public.actividades_grupales (
    id VARCHAR(64) PRIMARY KEY,
    profesor_id VARCHAR(64) NOT NULL REFERENCES public.profesionales(id) ON DELETE RESTRICT,
    cancha_id VARCHAR(64) REFERENCES public.canchas(id) ON DELETE SET NULL,
    nombre VARCHAR(200) NOT NULL,
    tipo_actividad VARCHAR(30) NOT NULL CHECK (tipo_actividad IN ('clase', 'entrenamiento')),
    nombre_profesional VARCHAR(150) NOT NULL,
    cancha_asignada VARCHAR(150),
    dias_semana VARCHAR(150) NOT NULL,
    horario_franja VARCHAR(100) NOT NULL,
    cupo_maximo INT NOT NULL DEFAULT 30 CHECK (cupo_maximo BETWEEN 1 AND 30),
    inscriptos_actuales INT NOT NULL DEFAULT 0 CHECK (inscriptos_actuales >= 0 AND inscriptos_actuales <= cupo_maximo),
    arancel_mensual NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (arancel_mensual >= 0),
    periodo_mes VARCHAR(50),
    estado VARCHAR(30) NOT NULL DEFAULT 'activa' CHECK (estado IN ('activa', 'finalizada', 'suspendida')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.actividades_grupales IS 'Entidad base TPT para Clases y Entrenamientos grupales (RD07-RD10).';

-- 5.24. Subclase TPT: Clase (public.clases : public.actividades_grupales — RF066-RF070)
CREATE TABLE IF NOT EXISTS public.clases (
    id VARCHAR(64) PRIMARY KEY REFERENCES public.actividades_grupales(id) ON DELETE CASCADE,
    nivel_escuela VARCHAR(80) NOT NULL DEFAULT 'Intermedio',
    rango_edad VARCHAR(80) NOT NULL DEFAULT 'Adultos',
    objetivo_pedagogico TEXT,
    incluye_materiales BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.clases IS 'Subclase TPT de ActividadGrupal dictada por un Profesor.';

-- 5.25. Subclase TPT: Entrenamiento (public.entrenamientos : public.actividades_grupales — RF079-RF083)
CREATE TABLE IF NOT EXISTS public.entrenamientos (
    id VARCHAR(64) PRIMARY KEY REFERENCES public.actividades_grupales(id) ON DELETE CASCADE,
    enfoque_entrenamiento VARCHAR(120) NOT NULL DEFAULT 'Físico-Técnico',
    nivel_intensidad VARCHAR(50) NOT NULL DEFAULT 'Alta',
    plan_fisico_asociado TEXT,
    requiere_apto_competitivo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.entrenamientos IS 'Subclase TPT de ActividadGrupal dictada por un Entrenador.';

-- 5.26. Entidad: InscripcionActividad (public.inscripciones_actividad — RF071-RF074, RF084-RF087)
CREATE TABLE IF NOT EXISTS public.inscripciones_actividad (
    id VARCHAR(64) PRIMARY KEY,
    actividad_grupal_id VARCHAR(64) NOT NULL REFERENCES public.actividades_grupales(id) ON DELETE CASCADE,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    nombre_alumno VARCHAR(150) NOT NULL,
    dni_alumno VARCHAR(20) NOT NULL,
    telefono_alumno VARCHAR(50),
    fecha_inscripcion DATE NOT NULL DEFAULT CURRENT_DATE,
    estado VARCHAR(30) NOT NULL DEFAULT 'activa' CHECK (estado IN ('activa', 'baja', 'finalizada')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inscripcion_actividad_alumno UNIQUE (actividad_grupal_id, cliente_id)
);

COMMENT ON TABLE public.inscripciones_actividad IS 'Inscripciones de alumnos a clases o entrenamientos grupales.';

-- 5.27. Entidad: Asistencia (public.asistencias — RF092-RF099)
CREATE TABLE IF NOT EXISTS public.asistencias (
    id VARCHAR(64) PRIMARY KEY,
    actividad_grupal_id VARCHAR(64) NOT NULL REFERENCES public.actividades_grupales(id) ON DELETE CASCADE,
    inscripcion_actividad_id VARCHAR(64) NOT NULL REFERENCES public.inscripciones_actividad(id) ON DELETE CASCADE,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    numero_sesion INT NOT NULL CHECK (numero_sesion >= 0),
    etiqueta_sesion VARCHAR(30) NOT NULL,
    fecha_sesion DATE NOT NULL DEFAULT CURRENT_DATE,
    estado_asistencia VARCHAR(5) NOT NULL DEFAULT 'P' CHECK (estado_asistencia IN ('P', 'A', 'J', '-')),
    observaciones VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.asistencias IS 'Control de asistencia por sesión de cada alumno inscripto.';

-- 5.28. Entidad: TurnoParticular (public.turnos_particulares — RF075-RF078, RF088-RF091)
CREATE TABLE IF NOT EXISTS public.turnos_particulares (
    id VARCHAR(64) PRIMARY KEY,
    profesor_id VARCHAR(64) NOT NULL REFERENCES public.profesionales(id) ON DELETE RESTRICT,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    cancha_id VARCHAR(64) REFERENCES public.canchas(id) ON DELETE SET NULL,
    modalidad VARCHAR(30) NOT NULL DEFAULT 'clase' CHECK (modalidad IN ('clase', 'entrenamiento')),
    nombre_profesional VARCHAR(150) NOT NULL,
    nombre_alumno VARCHAR(150) NOT NULL,
    dni_alumno VARCHAR(20) NOT NULL,
    telefono_alumno VARCHAR(50),
    cancha_asignada VARCHAR(150),
    fecha DATE NOT NULL,
    franja_horaria VARCHAR(50) NOT NULL,
    hora_inicio TIME NOT NULL DEFAULT '10:00',
    hora_fin TIME NOT NULL DEFAULT '11:00' CHECK (hora_fin > hora_inicio),
    tarifa_hora NUMERIC(12,2) NOT NULL DEFAULT 6000.00 CHECK (tarifa_hora >= 0),
    estado VARCHAR(30) NOT NULL DEFAULT 'programada' CHECK (estado IN ('programada', 'confirmada', 'cancelada', 'finalizada')),
    notas TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.turnos_particulares IS 'Clases y entrenamientos particulares solicitados a un profesional habilitado.';

-- =============================================================================
-- MÓDULO 6: FACTURACIÓN, PAGOS, RECIBOS, REEMBOLSOS Y DESCUENTOS
-- =============================================================================

-- 6.29. Entidad: Descuento (public.descuentos — RF138-RF142)
CREATE TABLE IF NOT EXISTS public.descuentos (
    id VARCHAR(64) PRIMARY KEY,
    codigo VARCHAR(30) UNIQUE NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    porcentaje_descuento NUMERIC(5,2) NOT NULL CHECK (porcentaje_descuento BETWEEN 0 AND 100),
    rubro_aplicable VARCHAR(100) NOT NULL DEFAULT 'Alquiler de Canchas',
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.descuentos IS 'Catálogo de beneficios y descuentos promocionales para socios.';

-- 6.30. Tabla Puente N:M: ClienteDescuento (public.cliente_descuentos)
CREATE TABLE IF NOT EXISTS public.cliente_descuentos (
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE CASCADE,
    descuento_id VARCHAR(64) NOT NULL REFERENCES public.descuentos(id) ON DELETE CASCADE,
    fecha_asignacion TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (cliente_id, descuento_id)
);

COMMENT ON TABLE public.cliente_descuentos IS 'Asignación N:M de descuentos activos a clientes.';

-- 6.31. Entidad: Pago (public.pagos — RF128-RF130, RD01, RD11)
CREATE TABLE IF NOT EXISTS public.pagos (
    id VARCHAR(64) PRIMARY KEY,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    reserva_id VARCHAR(64) REFERENCES public.reservas(id) ON DELETE RESTRICT,
    inscripcion_competencia_id VARCHAR(64) REFERENCES public.inscripciones_competencia(id) ON DELETE SET NULL,
    inscripcion_actividad_id VARCHAR(64) REFERENCES public.inscripciones_actividad(id) ON DELETE SET NULL,
    turno_particular_id VARCHAR(64) REFERENCES public.turnos_particulares(id) ON DELETE SET NULL,
    tipo_pago VARCHAR(30) NOT NULL CHECK (tipo_pago IN ('sena_50', 'saldo_50', 'inscripcion', 'cuota_clase', 'turno_particular', 'cargo_cancelacion')),
    medio_pago VARCHAR(50) NOT NULL CHECK (medio_pago IN ('Mercado Pago', 'Mercado Pago (QR)', 'Tarjeta de Débito', 'Tarjeta de Crédito', 'Billetera Virtual', 'Efectivo', 'Retención sobre seña', 'Crédito en cuenta de socio')),
    monto NUMERIC(12,2) NOT NULL CHECK (monto > 0),
    estado VARCHAR(30) NOT NULL DEFAULT 'completado' CHECK (estado IN ('pendiente', 'completado', 'rechazado', 'reembolsado')),
    mp_preference_id VARCHAR(120),
    mp_payment_id VARCHAR(120),
    mp_status VARCHAR(50),
    qr_data TEXT,
    payment_url TEXT,
    fecha_pago TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.pagos IS 'Transacciones de cobro (seña 50%, saldo 50%, inscripciones, cuotas) e integración con Mercado Pago (RD11).';

-- 6.32. Entidad: Recibo (public.recibos — RF134-RF137)
CREATE TABLE IF NOT EXISTS public.recibos (
    id VARCHAR(64) PRIMARY KEY,
    numero_recibo VARCHAR(64) UNIQUE NOT NULL,
    pago_id VARCHAR(64) NOT NULL REFERENCES public.pagos(id) ON DELETE RESTRICT,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    reserva_id VARCHAR(64) REFERENCES public.reservas(id) ON DELETE SET NULL,
    cliente_nombre VARCHAR(150) NOT NULL,
    cliente_dni VARCHAR(20) NOT NULL,
    concepto VARCHAR(255) NOT NULL,
    items JSONB NOT NULL DEFAULT '[]'::jsonb,
    total_abonado NUMERIC(12,2) NOT NULL CHECK (total_abonado > 0),
    medio_pago VARCHAR(50) NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'completado' CHECK (estado IN ('completado', 'anulado')),
    qr_data TEXT,
    cajero_nombre VARCHAR(100) NOT NULL DEFAULT 'Cobro Digital Online',
    fecha_emision TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.recibos IS 'Comprobantes oficiales de pago emitidos por el club.';

-- 6.33. Entidad: Factura (public.facturas — RF125-RF127)
CREATE TABLE IF NOT EXISTS public.facturas (
    id VARCHAR(64) PRIMARY KEY,
    numero_factura VARCHAR(64) UNIQUE NOT NULL,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    recibo_relacionado_id VARCHAR(64) REFERENCES public.recibos(id) ON DELETE SET NULL,
    pago_id VARCHAR(64) REFERENCES public.pagos(id) ON DELETE SET NULL,
    tipo_comprobante VARCHAR(5) NOT NULL DEFAULT 'B' CHECK (tipo_comprobante IN ('A', 'B', 'C')),
    cliente_nombre VARCHAR(150) NOT NULL,
    cliente_dni VARCHAR(25) NOT NULL,
    cliente_direccion VARCHAR(255),
    concepto VARCHAR(255) NOT NULL,
    items JSONB NOT NULL DEFAULT '[]'::jsonb,
    subtotal NUMERIC(12,2) NOT NULL CHECK (subtotal > 0),
    alicuota_iva NUMERIC(5,2) NOT NULL DEFAULT 21.00 CHECK (alicuota_iva >= 0),
    monto_iva NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (monto_iva >= 0),
    total NUMERIC(12,2) NOT NULL CHECK (total > 0),
    medio_pago VARCHAR(50) NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'emitida' CHECK (estado IN ('emitida', 'anulada')),
    emitida_por VARCHAR(100) NOT NULL DEFAULT 'Administración General',
    fecha_emision TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.facturas IS 'Facturas fiscales emitidas asociadas a pagos y recibos.';

-- 6.34. Entidad: Reembolso (public.reembolsos — RF131-RF133, RD02-RD04)
CREATE TABLE IF NOT EXISTS public.reembolsos (
    id VARCHAR(64) PRIMARY KEY,
    numero_reembolso VARCHAR(64) UNIQUE NOT NULL,
    cliente_id VARCHAR(64) NOT NULL REFERENCES public.clientes(id) ON DELETE RESTRICT,
    reserva_relacionada_id VARCHAR(64) REFERENCES public.reservas(id) ON DELETE SET NULL,
    recibo_relacionado_id VARCHAR(64) REFERENCES public.recibos(id) ON DELETE SET NULL,
    pago_relacionado_id VARCHAR(64) REFERENCES public.pagos(id) ON DELETE SET NULL,
    cliente_nombre VARCHAR(150) NOT NULL,
    cliente_dni VARCHAR(20) NOT NULL,
    motivo TEXT NOT NULL,
    tipo_reembolso VARCHAR(20) NOT NULL CHECK (tipo_reembolso IN ('total', 'parcial')),
    monto_original NUMERIC(12,2) NOT NULL CHECK (monto_original > 0),
    monto_reembolsado NUMERIC(12,2) NOT NULL CHECK (monto_reembolsado > 0 AND monto_reembolsado <= monto_original),
    medio_devolucion VARCHAR(100) NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'procesado' CHECK (estado IN ('pendiente', 'procesado', 'rechazado')),
    procesado_por VARCHAR(100) NOT NULL DEFAULT 'Sistema de Reservas',
    fecha_reembolso TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE public.reembolsos IS 'Reintegros totales o parciales por cancelación de reservas (RD02-RD04).';

-- =============================================================================
-- 3. ÍNDICES EN TODAS LAS CLAVES FORÁNEAS Y COLUMNAS CRÍTICAS DE RLS
-- =============================================================================
CREATE INDEX IF NOT EXISTS idx_usuarios_auth_user_id ON public.usuarios(auth_user_id);
CREATE INDEX IF NOT EXISTS idx_usuarios_rol ON public.usuarios(rol);
CREATE INDEX IF NOT EXISTS idx_certificados_profesor_id ON public.certificados(profesor_id);
CREATE INDEX IF NOT EXISTS idx_horarios_profesional_profesor_id ON public.horarios_profesional(profesor_id);

CREATE INDEX IF NOT EXISTS idx_canchas_tipo_cancha_id ON public.canchas(tipo_cancha_id);
CREATE INDEX IF NOT EXISTS idx_canchas_estado ON public.canchas(estado);
CREATE INDEX IF NOT EXISTS idx_horarios_cancha_cancha_id ON public.horarios_cancha(cancha_id);
CREATE INDEX IF NOT EXISTS idx_bloqueos_mantenimiento_cancha_id ON public.bloqueos_mantenimiento(cancha_id);
CREATE INDEX IF NOT EXISTS idx_bloqueos_mantenimiento_creado_por_id ON public.bloqueos_mantenimiento(creado_por_id);

CREATE INDEX IF NOT EXISTS idx_productos_categoria ON public.productos(categoria);
CREATE INDEX IF NOT EXISTS idx_stock_producto_id ON public.stock(producto_id);
CREATE INDEX IF NOT EXISTS idx_reservas_cancha_id ON public.reservas(cancha_id);
CREATE INDEX IF NOT EXISTS idx_reservas_cliente_id ON public.reservas(cliente_id);
CREATE INDEX IF NOT EXISTS idx_reservas_politica_id ON public.reservas(politica_reserva_id);
CREATE INDEX IF NOT EXISTS idx_reservas_cancha_fecha_estado ON public.reservas(cancha_id, fecha, estado);
CREATE INDEX IF NOT EXISTS idx_asignaciones_reserva_id ON public.asignaciones_equipamiento(reserva_id);
CREATE INDEX IF NOT EXISTS idx_asignaciones_producto_id ON public.asignaciones_equipamiento(producto_id);
CREATE INDEX IF NOT EXISTS idx_asignaciones_stock_id ON public.asignaciones_equipamiento(stock_id);
CREATE INDEX IF NOT EXISTS idx_participantes_reserva_id ON public.participantes(reserva_id);
CREATE INDEX IF NOT EXISTS idx_participantes_cliente_id ON public.participantes(cliente_id);

CREATE INDEX IF NOT EXISTS idx_competencias_tipo_id ON public.competencias(tipo_competencia_id);
CREATE INDEX IF NOT EXISTS idx_competencias_reglamento_id ON public.competencias(reglamento_id);
CREATE INDEX IF NOT EXISTS idx_equipos_competencia_id ON public.equipos(competencia_id);
CREATE INDEX IF NOT EXISTS idx_equipos_titular_id ON public.equipos(jugador_titular_id);
CREATE INDEX IF NOT EXISTS idx_equipos_pareja_id ON public.equipos(jugador_pareja_id);
CREATE INDEX IF NOT EXISTS idx_inscripciones_comp_competencia_id ON public.inscripciones_competencia(competencia_id);
CREATE INDEX IF NOT EXISTS idx_inscripciones_comp_cliente_id ON public.inscripciones_competencia(cliente_id);
CREATE INDEX IF NOT EXISTS idx_inscripciones_comp_equipo_id ON public.inscripciones_competencia(equipo_id);
CREATE INDEX IF NOT EXISTS idx_enfrentamientos_competencia_id ON public.enfrentamientos(competencia_id);
CREATE INDEX IF NOT EXISTS idx_enfrentamientos_cancha_id ON public.enfrentamientos(cancha_id);
CREATE INDEX IF NOT EXISTS idx_enfrentamientos_local_id ON public.enfrentamientos(equipo_local_id);
CREATE INDEX IF NOT EXISTS idx_enfrentamientos_visitante_id ON public.enfrentamientos(equipo_visitante_id);
CREATE INDEX IF NOT EXISTS idx_enfrentamientos_ganador_id ON public.enfrentamientos(equipo_ganador_id);

CREATE INDEX IF NOT EXISTS idx_actividades_profesor_id ON public.actividades_grupales(profesor_id);
CREATE INDEX IF NOT EXISTS idx_actividades_cancha_id ON public.actividades_grupales(cancha_id);
CREATE INDEX IF NOT EXISTS idx_inscripciones_act_actividad_id ON public.inscripciones_actividad(actividad_grupal_id);
CREATE INDEX IF NOT EXISTS idx_inscripciones_act_cliente_id ON public.inscripciones_actividad(cliente_id);
CREATE INDEX IF NOT EXISTS idx_asistencias_actividad_id ON public.asistencias(actividad_grupal_id);
CREATE INDEX IF NOT EXISTS idx_asistencias_inscripcion_id ON public.asistencias(inscripcion_actividad_id);
CREATE INDEX IF NOT EXISTS idx_asistencias_cliente_id ON public.asistencias(cliente_id);
CREATE INDEX IF NOT EXISTS idx_turnos_particulares_profesor_id ON public.turnos_particulares(profesor_id);
CREATE INDEX IF NOT EXISTS idx_turnos_particulares_cliente_id ON public.turnos_particulares(cliente_id);
CREATE INDEX IF NOT EXISTS idx_turnos_particulares_cancha_id ON public.turnos_particulares(cancha_id);

CREATE INDEX IF NOT EXISTS idx_cliente_descuentos_descuento_id ON public.cliente_descuentos(descuento_id);
CREATE INDEX IF NOT EXISTS idx_pagos_cliente_id ON public.pagos(cliente_id);
CREATE INDEX IF NOT EXISTS idx_pagos_reserva_id ON public.pagos(reserva_id);
CREATE INDEX IF NOT EXISTS idx_pagos_inscripcion_comp_id ON public.pagos(inscripcion_competencia_id);
CREATE INDEX IF NOT EXISTS idx_pagos_inscripcion_act_id ON public.pagos(inscripcion_actividad_id);
CREATE INDEX IF NOT EXISTS idx_pagos_turno_particular_id ON public.pagos(turno_particular_id);
CREATE INDEX IF NOT EXISTS idx_recibos_pago_id ON public.recibos(pago_id);
CREATE INDEX IF NOT EXISTS idx_recibos_cliente_id ON public.recibos(cliente_id);
CREATE INDEX IF NOT EXISTS idx_recibos_reserva_id ON public.recibos(reserva_id);
CREATE INDEX IF NOT EXISTS idx_facturas_cliente_id ON public.facturas(cliente_id);
CREATE INDEX IF NOT EXISTS idx_facturas_recibo_id ON public.facturas(recibo_relacionado_id);
CREATE INDEX IF NOT EXISTS idx_facturas_pago_id ON public.facturas(pago_id);
CREATE INDEX IF NOT EXISTS idx_reembolsos_cliente_id ON public.reembolsos(cliente_id);
CREATE INDEX IF NOT EXISTS idx_reembolsos_reserva_id ON public.reembolsos(reserva_relacionada_id);
CREATE INDEX IF NOT EXISTS idx_reembolsos_recibo_id ON public.reembolsos(recibo_relacionado_id);
CREATE INDEX IF NOT EXISTS idx_reembolsos_pago_id ON public.reembolsos(pago_relacionado_id);

-- =============================================================================
-- 4. REGLAS DE DOMINIO ESTRICTAS EN PL/pgSQL (IEEE SRS-830)
-- =============================================================================

-- 4.1. Validación de Reglas de Reserva (Duración <= 2h, Antelación <= 30 días, Mantenimiento y Solapamiento)
CREATE OR REPLACE FUNCTION public.fn_validar_reglas_reserva()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_horas_calculadas NUMERIC(4,2);
    v_politica RECORD;
    v_estado_cancha VARCHAR(30);
    v_reserva_solapada VARCHAR(64);
BEGIN
    SELECT duracion_maxima_horas, dias_max_antelacion, porcentaje_sena_obligatoria
      INTO v_politica
      FROM public.politicas_reserva
     WHERE id = NEW.politica_reserva_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'La política de reserva especificada (%) no existe.', NEW.politica_reserva_id
            USING ERRCODE = 'P0001';
    END IF;

    IF NEW.hora_fin <= NEW.hora_inicio THEN
        RAISE EXCEPTION 'La hora de finalización (%) debe ser posterior a la hora de inicio (%).', NEW.hora_fin, NEW.hora_inicio
            USING ERRCODE = 'P0001';
    END IF;

    v_horas_calculadas := ROUND((EXTRACT(EPOCH FROM (NEW.hora_fin - NEW.hora_inicio)) / 3600.0)::numeric, 2);

    IF v_horas_calculadas <= 0 OR v_horas_calculadas > v_politica.duracion_maxima_horas OR NEW.duracion_horas > 2.00 THEN
        RAISE EXCEPTION 'Regla de negocio violada: La duración máxima permitida por reserva es de % horas (solicitado: % h).',
            v_politica.duracion_maxima_horas, v_horas_calculadas
            USING ERRCODE = 'P0001';
    END IF;

    -- Antelación máxima de 30 días calendario (RD05)
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.fecha IS DISTINCT FROM OLD.fecha) THEN
        IF NEW.fecha < CURRENT_DATE THEN
            RAISE EXCEPTION 'RD05 violada: No se permiten reservas en fechas pasadas (%).', NEW.fecha
                USING ERRCODE = 'P0001';
        END IF;

        IF NEW.fecha > (CURRENT_DATE + (v_politica.dias_max_antelacion || ' days')::interval)::date THEN
            RAISE EXCEPTION 'RD05 violada: La antelación máxima permitida para reservar es de % días calendario.',
                v_politica.dias_max_antelacion
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    -- Cobro obligatorio del 50% de seña al reservar (RD01) y 50% restante al finalizar
    IF NEW.sena_pagada < ROUND(NEW.costo_total * (v_politica.porcentaje_sena_obligatoria / 100.0), 2) THEN
        RAISE EXCEPTION 'RD01 violada: Es obligatorio abonar al menos el 50%% de seña al reservar.'
            USING ERRCODE = 'P0001';
    END IF;

    IF NEW.estado = 'finalizada' AND (NEW.saldo_abonado IS DISTINCT FROM TRUE OR NEW.saldo_pendiente > 0) THEN
        RAISE EXCEPTION 'Regla de cobro violada: Para finalizar la reserva es obligatorio haber procesado el 50%% restante.'
            USING ERRCODE = 'P0001';
    END IF;

    -- Bloqueo por mantenimiento y no solapamiento (RD06)
    IF NEW.estado IN ('pendiente_pago', 'confirmada') THEN
        SELECT c.estado INTO v_estado_cancha FROM public.canchas c WHERE c.id = NEW.cancha_id;

        IF v_estado_cancha = 'mantenimiento' THEN
            RAISE EXCEPTION 'RD06 violada: La cancha % se encuentra en mantenimiento y no admite reservas.', NEW.cancha_id
                USING ERRCODE = 'P0001';
        END IF;

        SELECT r.id
          INTO v_reserva_solapada
          FROM public.reservas r
         WHERE r.cancha_id = NEW.cancha_id
           AND r.fecha = NEW.fecha
           AND r.id <> NEW.id
           AND r.estado IN ('pendiente_pago', 'confirmada')
           AND (NEW.hora_inicio < r.hora_fin AND NEW.hora_fin > r.hora_inicio)
         LIMIT 1;

        IF FOUND THEN
            RAISE EXCEPTION 'RD06 violada: Existe solapamiento horario en la cancha % con la reserva activa %.',
                NEW.cancha_id, v_reserva_solapada
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_validar_reglas_reserva ON public.reservas;
CREATE TRIGGER trg_validar_reglas_reserva
    BEFORE INSERT OR UPDATE ON public.reservas
    FOR EACH ROW EXECUTE FUNCTION public.fn_validar_reglas_reserva();

-- 4.2. Validación de Profesionales Habilitados y Cupo de Actividades Grupales (RD07-RD10)
CREATE OR REPLACE FUNCTION public.fn_validar_cupo_y_profesor_actividad()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_prof RECORD;
    v_cert_vigente BOOLEAN;
    v_max_alumnos_superficie INT;
BEGIN
    SELECT p.id, p.tipo_profesional, p.posee_licencia_oficial, u.nombre_completo
      INTO v_prof
      FROM public.profesionales p
      JOIN public.usuarios u ON u.id = p.id
     WHERE p.id = NEW.profesor_id;

    IF NOT FOUND OR v_prof.posee_licencia_oficial IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'RD08/RD09 violada: El profesional asignado (%) no posee licencia oficial activa.', NEW.profesor_id
            USING ERRCODE = 'P0001';
    END IF;

    SELECT EXISTS (
        SELECT 1
          FROM public.certificados c
         WHERE c.profesor_id = NEW.profesor_id
           AND c.estado = 'habilitado'
           AND c.fecha_vencimiento >= CURRENT_DATE
    ) INTO v_cert_vigente;

    IF NOT v_cert_vigente THEN
        RAISE EXCEPTION 'RD09 violada: El profesional % no posee un certificado habilitante vigente.', v_prof.nombre_completo
            USING ERRCODE = 'P0001';
    END IF;

    IF NEW.cancha_id IS NOT NULL THEN
        SELECT tc.max_alumnos_por_clase
          INTO v_max_alumnos_superficie
          FROM public.canchas ca
          JOIN public.tipos_cancha tc ON tc.id = ca.tipo_cancha_id
         WHERE ca.id = NEW.cancha_id;

        IF FOUND AND NEW.cupo_maximo > v_max_alumnos_superficie THEN
            RAISE EXCEPTION 'RD07/RD10 violada: El cupo máximo (%) supera el límite permitido para el tipo de cancha (% alumnos).',
                NEW.cupo_maximo, v_max_alumnos_superficie
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_validar_cupo_y_profesor_actividad ON public.actividades_grupales;
CREATE TRIGGER trg_validar_cupo_y_profesor_actividad
    BEFORE INSERT OR UPDATE ON public.actividades_grupales
    FOR EACH ROW EXECUTE FUNCTION public.fn_validar_cupo_y_profesor_actividad();

-- 4.3. Validación de Stock en Asignación de Equipamiento (Bloqueo estricto si Stock == 0)
CREATE OR REPLACE FUNCTION public.fn_gestionar_stock_asignacion()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_stock RECORD;
BEGIN
    IF TG_OP = 'INSERT' THEN
        SELECT id, stock_total, stock_en_uso, stock_disponible
          INTO v_stock
          FROM public.stock
         WHERE id = NEW.stock_id
           FOR UPDATE;

        IF NOT FOUND OR v_stock.stock_disponible <= 0 THEN
            RAISE EXCEPTION 'Reserva bloqueada por falta de stock: el ítem % tiene stock disponible cero.', NEW.stock_id
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_gestionar_stock_asignacion ON public.asignaciones_equipamiento;
CREATE TRIGGER trg_gestionar_stock_asignacion
    BEFORE INSERT ON public.asignaciones_equipamiento
    FOR EACH ROW EXECUTE FUNCTION public.fn_gestionar_stock_asignacion();

COMMIT;

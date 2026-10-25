-- =============================================================================
-- PROYECTO: Tenis Ahora (IEEE SRS-830)
-- ARCHIVO: 003_seed_initial_data.sql
-- DESCRIPCIÓN: Datos semilla iniciales idempotentes (ON CONFLICT DO NOTHING)
--              para los 6 Módulos y jerarquías TPT basados en seed.json.
-- MOTOR: Supabase (PostgreSQL 15+)
-- =============================================================================

BEGIN;

-- =============================================================================
-- MÓDULO 1: USUARIOS Y SUBCLASES TPT (CLIENTES, ADMINISTRADORES, PROFESIONALES)
-- =============================================================================
INSERT INTO public.usuarios (
    id, auth_user_id, nombre, apellido, nombre_completo, dni, telefono, email, direccion, rol, activo
) VALUES
('usr-admin', NULL, 'Carolina', 'Peralta', 'Carolina Peralta', '30.512.774', '+54 11 4253-8800', 'admin@tenisahora.com', 'Sede Club Tenis Ahora, Quilmes', 'admin', TRUE),
('usr-01', NULL, 'Juan', 'Sánchez', 'Juan Sánchez', '35.678.912', '+54 11 5234-7781', 'socio@tenisahora.com', 'Mitre 1240, Quilmes', 'client', TRUE),
('usr-02', NULL, 'Melina', 'Romo', 'Melina Romo', '39.104.556', '+54 11 6012-3345', 'melina.romo@gmail.com', 'Av. Calchaquí 2350, Quilmes', 'client', TRUE),
('coach-01', NULL, 'Gustavo', 'Ferrari', 'Gustavo Ferrari', '28.334.901', '+54 11 5566-7788', 'profesor@tenisahora.com', 'Quilmes, Buenos Aires', 'coach', TRUE),
('coach-02', NULL, 'Laura', 'Domínguez', 'Laura Domínguez', '31.245.678', '+54 11 4433-2211', 'entrenador@tenisahora.com', 'Quilmes, Buenos Aires', 'coach', TRUE),
('coach-03', NULL, 'Diego', 'Paredes', 'Diego Paredes', '29.871.340', '+54 11 6677-9922', 'diego.paredes@tenisahora.com', 'Quilmes, Buenos Aires', 'coach', TRUE)
ON CONFLICT (id) DO NOTHING;

-- Subclase TPT: Administradores
INSERT INTO public.administradores (
    id, legajo_empleado, cargo, departamento, nivel_acceso, fecha_ingreso
) VALUES
('usr-admin', 'ADM-001', 'Administradora General', 'Recepción y Caja', 3, '2022-01-01')
ON CONFLICT (id) DO NOTHING;

-- Subclase TPT: Clientes
INSERT INTO public.clientes (
    id, numero_socio, fecha_alta_socio, total_reservas, saldo_credito_favor, categoria_juego
) VALUES
('usr-01', 'TA-1001', '2024-03-10', 14, 0.00, 'Intermedio'),
('usr-02', 'TA-1002', '2024-05-02', 9, 0.00, 'Intermedio')
ON CONFLICT (id) DO NOTHING;

-- Subclase TPT: Profesionales
INSERT INTO public.profesionales (
    id, tipo_profesional, especialidad, anios_experiencia, posee_licencia_oficial, tarifa_hora_particular
) VALUES
('coach-01', 'Profesor', 'Escuela de Adultos y Menores', 12, TRUE, 6000.00),
('coach-02', 'Entrenador', 'Preparación Física y Dobles', 8, TRUE, 6000.00),
('coach-03', 'Entrenador', 'Alto Rendimiento Competitivo', 10, TRUE, 6000.00)
ON CONFLICT (id) DO NOTHING;

-- Certificados Oficiales AAT (RD08 / RD09)
INSERT INTO public.certificados (
    id, profesor_id, titulo_habilitante, institucion_emisora, numero_matricula, fecha_emision, fecha_vencimiento, estado, documento_nombre, documento_mime_type, documento_tamano_bytes
) VALUES
('cert-coach-01', 'coach-01', 'Profesor Nacional de Tenis', 'Asociación Argentina de Tenis (AAT)', 'AAT-PRO-41287', '2014-08-20', '2028-12-31', 'habilitado', 'Titulo_Profesor_Ferrari.pdf', 'application/pdf', 862),
('cert-coach-02', 'coach-02', 'Entrenadora Nacional de Tenis Nivel 1', 'Asociación Argentina de Tenis (AAT)', 'AAT-ENT-50932', '2018-03-12', '2029-03-31', 'habilitado', 'Certificado_Entrenadora_Dominguez.pdf', 'application/pdf', 873),
('cert-coach-03', 'coach-03', 'Entrenador Nacional de Tenis Nivel 2', 'Asociación Argentina de Tenis (AAT)', 'AAT-ENT-47710', '2016-09-10', '2027-12-31', 'habilitado', 'Certificado_Entrenador_Paredes.pdf', 'application/pdf', 870)
ON CONFLICT (id) DO NOTHING;

-- Horarios de Profesionales
INSERT INTO public.horarios_profesional (
    id, profesor_id, dia_semana, hora_inicio, hora_fin, disponible, observaciones
) VALUES
('hp-01', 'coach-01', 'Martes', '08:00', '21:00', TRUE, 'Horario de escuela y particulares'),
('hp-02', 'coach-01', 'Jueves', '08:00', '21:00', TRUE, 'Horario de escuela y particulares'),
('hp-03', 'coach-02', 'Lunes', '14:00', '20:00', TRUE, 'Entrenamientos físicos'),
('hp-04', 'coach-02', 'Miércoles', '14:00', '20:00', TRUE, 'Entrenamientos físicos'),
('hp-05', 'coach-03', 'Martes', '10:00', '22:00', TRUE, 'Alto rendimiento'),
('hp-06', 'coach-03', 'Jueves', '10:00', '22:00', TRUE, 'Alto rendimiento')
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- MÓDULO 2: TIPOS DE CANCHA, CANCHAS, HORARIOS Y BLOQUEOS DE MANTENIMIENTO
-- =============================================================================
INSERT INTO public.tipos_cancha (
    id, nombre, etiqueta, descripcion, velocidad_superficie, precio_base_hora, frecuencia_mantenimiento, max_alumnos_por_clase, activo
) VALUES
('ct-ladrillo', 'Ladrillo', 'Polvo de ladrillo', 'Superficie lenta de arcilla roja. Ideal para peloteo largo y juego de fondo.', 'Lenta', 4800.00, 'Diaria (riego y pasada de alfombra)', 30, TRUE),
('ct-cemento', 'Cemento', 'Cemento', 'Superficie dura de pique medio-rápido. Bajo mantenimiento.', 'Media-rápida', 4200.00, 'Semanal (barrido y revisión de fisuras)', 20, TRUE),
('ct-pasto', 'Pasto', 'Césped natural', 'Superficie rápida de pique bajo. No se usa en días de lluvia.', 'Rápida', 6500.00, 'Diaria (corte, riego y marcado de líneas)', 12, TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.canchas (
    id, tipo_cancha_id, nombre, superficie, capacidad_jugadores, precio_por_hora, estado, descripcion, notas_mantenimiento, foto_url
) VALUES
('cancha-1', 'ct-ladrillo', 'Cancha 1 - Lionel Messi', 'Ladrillo', 4, 4800.00, 'disponible', 'Cancha central de polvo de ladrillo, con tribuna.', '', 'https://images.unsplash.com/photo-1595435934249-5df7ed86e1c0?auto=format&fit=crop&w=600&q=80'),
('cancha-2', 'ct-ladrillo', 'Cancha 2 - Dibu Martínez', 'Ladrillo', 4, 4800.00, 'disponible', 'Polvo de ladrillo, ideal para singles y dobles.', '', 'https://images.unsplash.com/photo-1554068865-24cecd4e34b8?auto=format&fit=crop&w=600&q=80'),
('cancha-3', 'ct-cemento', 'Cancha 3 - Ángel Di María', 'Cemento', 4, 4200.00, 'disponible', 'Superficie dura de pique rápido y uniforme.', '', 'https://images.unsplash.com/photo-1622279457486-62dcc4a431d6?auto=format&fit=crop&w=600&q=80'),
('cancha-4', 'ct-cemento', 'Cancha 4 - Julián Álvarez', 'Cemento', 4, 4200.00, 'mantenimiento', 'Superficie dura. Actualmente en repintado de líneas.', 'Repintado de líneas y cambio de red hasta el 30/10.', 'https://images.unsplash.com/photo-1560012057-4372e14c5085?auto=format&fit=crop&w=600&q=80'),
('cancha-5', 'ct-pasto', 'Cancha 5 - Rodrigo De Paul', 'Pasto', 4, 6500.00, 'disponible', 'Césped natural, uso con calzado adecuado.', '', 'https://images.unsplash.com/photo-1534438327276-14e5300c3a48?auto=format&fit=crop&w=600&q=80')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.horarios_cancha (
    id, cancha_id, dia_semana, hora_apertura, hora_cierre, duracion_turno_minutos, habilitado, notas
) VALUES
('hc-c1-lun', 'cancha-1', 'Lunes', '08:00', '22:00', 60, TRUE, 'Horario corrido'),
('hc-c2-lun', 'cancha-2', 'Lunes', '08:00', '22:00', 60, TRUE, 'Horario corrido'),
('hc-c3-lun', 'cancha-3', 'Lunes', '08:00', '22:00', 60, TRUE, 'Horario corrido'),
('hc-c5-lun', 'cancha-5', 'Lunes', '08:00', '22:00', 60, TRUE, 'Cerrada días de lluvia')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.bloqueos_mantenimiento (
    id, cancha_id, dia_semana, fecha_inicio, fecha_fin, hora_inicio, hora_fin, motivo, activo, creado_por_id
) VALUES
('blq-c4-01', 'cancha-4', NULL, '2026-10-20', '2026-10-30', '08:00', '22:00', 'Repintado de líneas y cambio de red', TRUE, 'usr-admin')
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- MÓDULO 3: POLÍTICA DE RESERVA, PRODUCTOS Y STOCK
-- =============================================================================
INSERT INTO public.politicas_reserva (
    id, nombre_politica, duracion_maxima_horas, dias_max_antelacion, porcentaje_sena_obligatoria, horas_cancelacion_gratuita, porcentaje_retencion_tardia, bloqueo_estricto_stock_cero, activa
) VALUES
('pol-estandar', 'Política Oficial Club Tenis Ahora (IEEE SRS-830)', 2.00, 30, 50.00, 6.00, 50.00, TRUE, TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.productos (
    id, codigo_sku, nombre, categoria, descripcion, unidad_medida, precio_unitario, es_obligatorio_reserva, activo
) VALUES
('prod-redes', 'SKU-RED-01', 'Redes de tenis', 'Redes', 'Una por cancha habilitada; la restante es de repuesto.', 'unidades', 0.00, TRUE, TRUE),
('prod-pelotas', 'SKU-PEL-01', 'Tubos de pelotas', 'Pelotas', 'Tubos de 3 pelotas presurizadas. Se entrega uno por reserva.', 'tubos', 0.00, TRUE, TRUE),
('prod-raquetas', 'SKU-RAQ-01', 'Raquetas de préstamo', 'Raquetas', 'Raquetas para socios que no traen la propia.', 'raquetas', 0.00, TRUE, TRUE),
('prod-grips', 'SKU-ACC-01', 'Overgrips', 'Accesorios', 'Overgrips a la venta en recepción.', 'unidades', 2500.00, FALSE, TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.stock (
    id, producto_id, stock_total, stock_en_uso, stock_disponible, umbral_minimo_alerta, estado
) VALUES
('item-redes', 'prod-redes', 6, 5, 1, 1, 'disponible'),
('item-pelotas', 'prod-pelotas', 40, 12, 28, 5, 'disponible'),
('item-raquetas', 'prod-raquetas', 20, 6, 14, 4, 'disponible'),
('item-grips', 'prod-grips', 50, 0, 50, 10, 'disponible')
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- MÓDULO 4: TIPOS DE COMPETENCIA, REGLAMENTOS Y COMPETENCIAS
-- =============================================================================
INSERT INTO public.tipos_competencia (
    id, nombre, formato, aplica_a, sistema_puntuacion, min_participantes, max_participantes, descripcion, activo
) VALUES
('cmp-elim', 'Torneo de eliminación directa', 'Cuadro eliminatorio', 'Torneo', 'Al mejor de 3 sets con tie-break', 8, 16, 'El perdedor queda eliminado. Cuadro de octavos, cuartos, semifinales y final.', TRUE),
('cmp-liga', 'Liga todos contra todos', 'Round robin por puntos', 'Liga', '3 puntos por victoria, 0 por derrota. Desempate por diferencia de sets', 4, 8, 'Cada jugador enfrenta a todos los demás a lo largo de las jornadas.', TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.reglamentos (
    id, codigo_reglamento, titulo, normativa_base, texto_reglamento_interno
) VALUES
('reg-trn-01', 'REG-AAT-2026-01', 'Reglamento Oficial Torneo Primavera', 'Reglas Oficiales Asociación Argentina de Tenis (AAT)', 'Partidos al mejor de 3 sets con tie-break en todos los sets. Tolerancia de espera de 15 minutos; pasado ese tiempo se pierde el partido por W.O. Pelotas provistas por el club.'),
('reg-liga-01', 'REG-AAT-2026-02', 'Reglamento Oficial Liga Singles', 'Reglas Oficiales Asociación Argentina de Tenis (AAT)', 'Cada partido ganado suma 3 puntos. Partidos al mejor de 3 sets. Desempate en la tabla: 1) diferencia de sets, 2) resultado entre ambos.')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.competencias (
    id, tipo_competencia_id, reglamento_id, nombre, clase_competencia, categoria, modalidad, superficie_requerida, fecha_inicio, fecha_fin, costo_inscripcion, max_participantes, inscriptos_actuales, estado, descripcion
) VALUES
('trn-01', 'cmp-elim', 'reg-trn-01', 'Torneo Primavera 2026 Singles Caballeros', 'Torneo', 'Masculino', 'Singles', 'Ladrillo', '2026-10-17', '2026-11-01', 8000.00, 16, 16, 'en_curso', 'Torneo de eliminación directa a 16 jugadores en polvo de ladrillo.'),
('liga-01', 'cmp-liga', 'reg-liga-01', 'Liga Primavera Singles Caballeros', 'Liga', 'Masculino', 'Singles', 'Ladrillo', '2026-10-04', '2026-12-13', 15000.00, 8, 8, 'en_curso', 'Liga todos contra todos a una rueda en polvo de ladrillo.')
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- MÓDULO 5: ACTIVIDADES GRUPALES Y SUBCLASES TPT (CLASES Y ENTRENAMIENTOS)
-- =============================================================================
INSERT INTO public.actividades_grupales (
    id, profesor_id, cancha_id, nombre, tipo_actividad, nombre_profesional, cancha_asignada, dias_semana, horario_franja, cupo_maximo, inscriptos_actuales, arancel_mensual, periodo_mes, estado
) VALUES
('cls-01', 'coach-01', 'cancha-1', 'Escuela de Adultos - Nivel Intermedio', 'clase', 'Gustavo Ferrari', 'Cancha 1 (Ladrillo)', 'Martes y Jueves', '19:00 - 20:30', 12, 6, 16000.00, 'Octubre 2026', 'activa'),
('cls-02', 'coach-02', 'cancha-3', 'Iniciación Adultos', 'entrenamiento', 'Laura Domínguez', 'Cancha 3 (Cemento)', 'Lunes y Miércoles', '18:00 - 19:30', 10, 4, 14000.00, 'Octubre 2026', 'activa')
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.clases (
    id, nivel_escuela, rango_edad, objetivo_pedagogico, incluye_materiales
) VALUES
('cls-01', 'Intermedio', 'Adultos', 'Perfeccionamiento técnico de golpes de fondo y servicio.', TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.entrenamientos (
    id, enfoque_entrenamiento, nivel_intensidad, plan_fisico_asociado, requiere_apto_competitivo
) VALUES
('cls-02', 'Físico-Técnico', 'Media-Alta', 'Circuito de coordinación, velocidad de piernas y regularidad.', FALSE)
ON CONFLICT (id) DO NOTHING;

-- =============================================================================
-- MÓDULO 6: DESCUENTOS Y ASIGNACIÓN A CLIENTES
-- =============================================================================
INSERT INTO public.descuentos (
    id, codigo, nombre, porcentaje_descuento, rubro_aplicable, activo
) VALUES
('disc-01', 'PAQ10', 'Paquete 10 horas mensuales', 15.00, 'Alquiler de canchas', TRUE),
('disc-02', 'LIGA20', 'Jugador de liga', 20.00, 'Torneos y ligas', TRUE),
('disc-03', 'EST10', 'Estudiantes', 10.00, 'Clases y entrenamientos', TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.cliente_descuentos (
    cliente_id, descuento_id
) VALUES
('usr-01', 'disc-01'),
('usr-02', 'disc-02')
ON CONFLICT (cliente_id, descuento_id) DO NOTHING;

COMMIT;

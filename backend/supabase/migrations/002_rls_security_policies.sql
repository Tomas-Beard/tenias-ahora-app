-- =============================================================================
-- PROYECTO: Tenis Ahora (IEEE SRS-830)
-- ARCHIVO: 002_rls_security_policies.sql
-- DESCRIPCIÓN: Seguridad Row Level Security (RLS) en las 34 tablas de los
--              6 Módulos para los tres roles: Administrador ('admin'),
--              Usuario/Cliente ('client') y Profesor/Entrenador ('coach').
-- MOTOR: Supabase (PostgreSQL 15+)
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. ESQUEMA PRIVADO Y FUNCIONES HELPER DE IDENTIDAD / ROL (SECURITY DEFINER)
-- -----------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS private;

REVOKE ALL ON SCHEMA private FROM PUBLIC, anon;
GRANT USAGE ON SCHEMA private TO authenticated, service_role;

CREATE OR REPLACE FUNCTION private.get_my_role()
RETURNS VARCHAR(30)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_uid UUID;
    v_jwt JSONB;
    v_role VARCHAR(30);
BEGIN
    v_uid := (SELECT auth.uid());
    IF v_uid IS NULL THEN
        RETURN NULL;
    END IF;

    v_jwt := (SELECT auth.jwt());
    v_role := COALESCE(
        v_jwt -> 'app_metadata' ->> 'role',
        v_jwt -> 'user_metadata' ->> 'role'
    );

    IF v_role IN ('admin', 'client', 'coach') THEN
        RETURN v_role;
    END IF;

    SELECT u.rol
      INTO v_role
      FROM public.usuarios u
     WHERE u.auth_user_id = v_uid
     LIMIT 1;

    RETURN v_role;
END;
$$;

CREATE OR REPLACE FUNCTION private.get_my_user_id()
RETURNS VARCHAR(64)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
    SELECT u.id
      FROM public.usuarios u
     WHERE u.auth_user_id = (SELECT auth.uid())
     LIMIT 1;
$$;

REVOKE EXECUTE ON FUNCTION private.get_my_role() FROM PUBLIC, anon;
REVOKE EXECUTE ON FUNCTION private.get_my_user_id() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION private.get_my_role() TO authenticated, service_role;
GRANT EXECUTE ON FUNCTION private.get_my_user_id() TO authenticated, service_role;

-- -----------------------------------------------------------------------------
-- 2. HABILITAR Y FORZAR RLS EN LAS 34 TABLAS DE LOS 6 MÓDULOS
-- -----------------------------------------------------------------------------
DO $$
DECLARE
    v_table TEXT;
    v_tables TEXT[] := ARRAY[
        -- Módulo 1
        'usuarios', 'clientes', 'administradores', 'profesionales', 'certificados', 'horarios_profesional',
        -- Módulo 2
        'tipos_cancha', 'canchas', 'horarios_cancha', 'bloqueos_mantenimiento',
        -- Módulo 3
        'politicas_reserva', 'productos', 'stock', 'reservas', 'asignaciones_equipamiento', 'participantes',
        -- Módulo 4
        'tipos_competencia', 'reglamentos', 'competencias', 'equipos', 'inscripciones_competencia', 'enfrentamientos',
        -- Módulo 5
        'actividades_grupales', 'clases', 'entrenamientos', 'inscripciones_actividad', 'asistencias', 'turnos_particulares',
        -- Módulo 6
        'descuentos', 'cliente_descuentos', 'pagos', 'recibos', 'facturas', 'reembolsos'
    ];
BEGIN
    FOREACH v_table IN ARRAY v_tables LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY;', v_table);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY;', v_table);

        -- Política transversal de acceso total para el rol Administrador ('admin')
        EXECUTE format('DROP POLICY IF EXISTS rls_%I_admin_all ON public.%I;', v_table, v_table);
        EXECUTE format(
            'CREATE POLICY rls_%I_admin_all ON public.%I FOR ALL TO authenticated USING ((SELECT private.get_my_role()) = ''admin'') WITH CHECK ((SELECT private.get_my_role()) = ''admin'');',
            v_table, v_table
        );
    END LOOP;
END;
$$;

-- -----------------------------------------------------------------------------
-- 3. POLÍTICAS DE LECTURA DE CATÁLOGOS PARA 'client' Y 'coach'
-- -----------------------------------------------------------------------------
DO $$
DECLARE
    v_cat TEXT;
    v_catalogs TEXT[] := ARRAY[
        'profesionales', 'certificados', 'horarios_profesional',
        'tipos_cancha', 'canchas', 'horarios_cancha', 'bloqueos_mantenimiento',
        'politicas_reserva', 'productos', 'stock',
        'tipos_competencia', 'reglamentos', 'competencias', 'equipos', 'enfrentamientos',
        'actividades_grupales', 'clases', 'entrenamientos', 'descuentos'
    ];
BEGIN
    FOREACH v_cat IN ARRAY v_catalogs LOOP
        EXECUTE format('DROP POLICY IF EXISTS rls_%I_catalog_select ON public.%I;', v_cat, v_cat);
        EXECUTE format(
            'CREATE POLICY rls_%I_catalog_select ON public.%I FOR SELECT TO authenticated USING ((SELECT private.get_my_role()) IN (''client'', ''coach''));',
            v_cat, v_cat
        );
    END LOOP;
END;
$$;

-- -----------------------------------------------------------------------------
-- 4. POLÍTICAS ESPECÍFICAS DEL ROL CLIENTE ('client') Y PROFESIONAL ('coach')
-- -----------------------------------------------------------------------------

-- 4.1. Módulo 1: Usuarios, Clientes, Profesionales y Horarios
CREATE POLICY rls_usuarios_own_select ON public.usuarios
    FOR SELECT TO authenticated
    USING (auth_user_id = (SELECT auth.uid()) OR id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_usuarios_own_update ON public.usuarios
    FOR UPDATE TO authenticated
    USING (auth_user_id = (SELECT auth.uid()) OR id = (SELECT private.get_my_user_id()))
    WITH CHECK (auth_user_id = (SELECT auth.uid()) OR id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_clientes_own_select ON public.clientes
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_profesionales_coach_update ON public.profesionales
    FOR UPDATE TO authenticated
    USING ((SELECT private.get_my_role()) = 'coach' AND id = (SELECT private.get_my_user_id()))
    WITH CHECK ((SELECT private.get_my_role()) = 'coach' AND id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_horarios_profesional_coach_manage ON public.horarios_profesional
    FOR ALL TO authenticated
    USING ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()))
    WITH CHECK ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()));

-- 4.2. Módulo 3: Reservas, Asignaciones de Equipamiento y Participantes
CREATE POLICY rls_reservas_client_select ON public.reservas
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_reservas_client_insert ON public.reservas
    FOR INSERT TO authenticated
    WITH CHECK ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_reservas_client_update ON public.reservas
    FOR UPDATE TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()))
    WITH CHECK ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_asignaciones_client_select ON public.asignaciones_equipamiento
    FOR SELECT TO authenticated
    USING (
        (SELECT private.get_my_role()) = 'client'
        AND EXISTS (
            SELECT 1 FROM public.reservas r
            WHERE r.id = asignaciones_equipamiento.reserva_id
              AND r.cliente_id = (SELECT private.get_my_user_id())
        )
    );

CREATE POLICY rls_asignaciones_client_insert ON public.asignaciones_equipamiento
    FOR INSERT TO authenticated
    WITH CHECK (
        (SELECT private.get_my_role()) = 'client'
        AND EXISTS (
            SELECT 1 FROM public.reservas r
            WHERE r.id = asignaciones_equipamiento.reserva_id
              AND r.cliente_id = (SELECT private.get_my_user_id())
        )
    );

CREATE POLICY rls_participantes_client_select ON public.participantes
    FOR SELECT TO authenticated
    USING (
        (SELECT private.get_my_role()) = 'client'
        AND (
            cliente_id = (SELECT private.get_my_user_id())
            OR EXISTS (
                SELECT 1 FROM public.reservas r
                WHERE r.id = participantes.reserva_id
                  AND r.cliente_id = (SELECT private.get_my_user_id())
            )
        )
    );

CREATE POLICY rls_participantes_client_insert ON public.participantes
    FOR INSERT TO authenticated
    WITH CHECK (
        (SELECT private.get_my_role()) = 'client'
        AND EXISTS (
            SELECT 1 FROM public.reservas r
            WHERE r.id = participantes.reserva_id
              AND r.cliente_id = (SELECT private.get_my_user_id())
        )
    );

-- 4.3. Módulo 4: Inscripciones a Competencias
CREATE POLICY rls_inscripciones_comp_client_select ON public.inscripciones_competencia
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_inscripciones_comp_client_insert ON public.inscripciones_competencia
    FOR INSERT TO authenticated
    WITH CHECK ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

-- 4.4. Módulo 5: Actividades Grupales, Asistencias y Turnos Particulares
CREATE POLICY rls_actividades_coach_manage ON public.actividades_grupales
    FOR ALL TO authenticated
    USING ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()))
    WITH CHECK ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_inscripciones_act_client_own ON public.inscripciones_actividad
    FOR SELECT TO authenticated
    USING (
        (SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id())
        OR (SELECT private.get_my_role()) = 'coach' AND EXISTS (
            SELECT 1 FROM public.actividades_grupales ag
            WHERE ag.id = inscripciones_actividad.actividad_grupal_id
              AND ag.profesor_id = (SELECT private.get_my_user_id())
        )
    );

CREATE POLICY rls_inscripciones_act_client_insert ON public.inscripciones_actividad
    FOR INSERT TO authenticated
    WITH CHECK ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_asistencias_coach_manage ON public.asistencias
    FOR ALL TO authenticated
    USING (
        (SELECT private.get_my_role()) = 'coach'
        AND EXISTS (
            SELECT 1 FROM public.actividades_grupales ag
            WHERE ag.id = asistencias.actividad_grupal_id
              AND ag.profesor_id = (SELECT private.get_my_user_id())
        )
    )
    WITH CHECK (
        (SELECT private.get_my_role()) = 'coach'
        AND EXISTS (
            SELECT 1 FROM public.actividades_grupales ag
            WHERE ag.id = asistencias.actividad_grupal_id
              AND ag.profesor_id = (SELECT private.get_my_user_id())
        )
    );

CREATE POLICY rls_asistencias_client_select ON public.asistencias
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_turnos_particulares_own ON public.turnos_particulares
    FOR ALL TO authenticated
    USING (
        ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()))
        OR ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()))
    )
    WITH CHECK (
        ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()))
        OR ((SELECT private.get_my_role()) = 'coach' AND profesor_id = (SELECT private.get_my_user_id()))
    );

-- 4.5. Módulo 6: Descuentos de Cliente, Pagos, Recibos, Facturas y Reembolsos
CREATE POLICY rls_cliente_descuentos_own ON public.cliente_descuentos
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_pagos_client_select ON public.pagos
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_pagos_client_insert ON public.pagos
    FOR INSERT TO authenticated
    WITH CHECK ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_recibos_client_select ON public.recibos
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_facturas_client_select ON public.facturas
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

CREATE POLICY rls_reembolsos_client_select ON public.reembolsos
    FOR SELECT TO authenticated
    USING ((SELECT private.get_my_role()) = 'client' AND cliente_id = (SELECT private.get_my_user_id()));

COMMIT;

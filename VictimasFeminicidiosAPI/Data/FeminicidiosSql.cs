namespace VictimasFeminicidiosAPI.Data;

internal static class FeminicidiosSql
{
    internal const string Consulta = """
        WITH sabanas AS (
            -- Claves distintas pueden producir los mismos textos: no multiplicar victimas por eso.
            SELECT DISTINCT id_modalidad_delito, id_grado_consumacion,
                id_instrumento_comision, id_forma_accion,
                delito_sabana, subtipo_delito_sabana, modalidad_delito_sabana
            FROM dbo.catalogo_delito_sabana WHERE activo = 1
        )
        SELECT
            v.id_victima AS IdRegistro,
            v.identificador_victima_fiscalia AS IdentificadorVictima,
            v.nombre_vicfem AS Nombre,
            v.primer_apellido_vicfem AS PrimerApellido,
            v.segundo_apellido_vicfem AS SegundoApellido,
            v.curp_vicfem AS CURP,
            tv.descripcion AS TipoVictima,
            tvm.descripcion AS TipoPersonaMoral,
            sx.descripcion AS Sexo,
            gen.descripcion AS Genero,
            pob.descripcion AS PoblacionIndigena,
            disc.descripcion AS Discapacidad,
            CONVERT(nvarchar(10), v.fecha_nacimiento, 23) AS FechaNacimiento,
            CONVERT(int, v.edad) AS Edad,
            nac.descripcion AS Nacionalidad,
            ef_reg.nombre AS EntidadReportante,
            u_v.usuario AS UsuarioReportante,
            CONVERT(int, c_ci.anio_corte) AS AnioCorte,
            CHOOSE(@Mes, N'ENERO', N'FEBRERO', N'MARZO', N'ABRIL', N'MAYO', N'JUNIO',
                N'JULIO', N'AGOSTO', N'SEPTIEMBRE', N'OCTUBRE', N'NOVIEMBRE', N'DICIEMBRE') AS MesCorte,
            ci.identificador_carpeta_fiscalia AS IdentificadorCarpeta,
            ci.nomenclatura_carpeta_fiscalia AS NumeroCarpeta,
            CONVERT(nvarchar(10), ci.fecha_inicio, 23) AS FechaInicioCarpeta,
            CASE WHEN CONVERT(time(0), ci.fecha_inicio) = '00:00:00' THEN N''
                ELSE CONVERT(nvarchar(8), ci.fecha_inicio, 108) END AS HoraInicioCarpeta,
            ci.resumen_hechos AS ResumenHechos,
            d.identificador_delito_fiscalia AS IdentificadorDelito,
            d.delito_fiscalia AS DelitoFiscalia,
            d.modalidad_delito_fiscalia AS ModalidadFiscalia,
            bj.bien_juridico AS BienJuridico,
            cd.delito AS DelitoCatalogo,
            sd.subtipo_delito AS SubtipoDelito,
            md.modalidad_delito AS ModalidadCatalogo,
            fa.descripcion AS FormaAccion,
            ic.descripcion AS InstrumentoComision,
            gc.descripcion AS GradoConsumacion,
            CONVERT(nvarchar(10), d.fecha_hechos, 23) AS FechaHechos,
            CONVERT(nvarchar(8), d.fecha_hechos, 108) AS HoraHechos,
            CASE WHEN sab.id_modalidad_delito IS NULL THEN NULL ELSE bj.bien_juridico END AS BienJuridicoSabana,
            sab.delito_sabana AS DelitoSabana,
            sab.subtipo_delito_sabana AS SubtipoDelitoSabana,
            sab.modalidad_delito_sabana AS ModalidadDelitoSabana,
            ef.nombre AS EntidadHechos,
            mun.nombre AS MunicipioHechos,
            d.localidad_fiscalia_nombre AS LocalidadHechos,
            d.colonia_fiscalia_nombre AS ColoniaHechos,
            COALESCE(NULLIF(LTRIM(RTRIM(d.codigo_postal_fiscalia)), N''), cp.codigo_postal) AS CodigoPostal,
            d.domicilio_hechos AS DomicilioHechos,
            CONVERT(nvarchar(50), d.coordenada_x) AS CoordenadaX,
            CONVERT(nvarchar(50), d.coordenada_y) AS CoordenadaY,
            ci.fecha_registro AS FechaRegistro
        FROM dbo.carpeta_investigacion ci
        INNER JOIN dbo.delito d ON d.id_carpeta_investigacion = ci.id_carpeta_investigacion
        INNER JOIN dbo.victima v ON v.id_delito = d.id_delito
        INNER JOIN dbo.carga c_ci ON c_ci.id_carga = ci.id_carga
        INNER JOIN dbo.usuario u_ci ON u_ci.id_usuario = c_ci.id_usuario_carga
        INNER JOIN dbo.carga c_d ON c_d.id_carga = d.id_carga
        INNER JOIN dbo.usuario u_d ON u_d.id_usuario = c_d.id_usuario_carga
        INNER JOIN dbo.carga c_v ON c_v.id_carga = v.id_carga
        INNER JOIN dbo.usuario u_v ON u_v.id_usuario = c_v.id_usuario_carga
        INNER JOIN dbo.catalogo_forma_accion fa ON fa.id_forma_accion = d.id_forma_accion
        INNER JOIN dbo.catalogo_instrumento_comision ic ON ic.id_instrumento_comision = d.id_instrumento_comision
        INNER JOIN dbo.catalogo_grado_consumacion gc ON gc.id_grado_consumacion = d.id_grado_consumacion
        INNER JOIN dbo.catalogo_modalidad_delito md ON md.id_modalidad_delito = d.id_modalidad_delito
        INNER JOIN dbo.catalogo_subtipo_delito sd ON sd.id_subtipo_delito = md.id_subtipo_delito
        INNER JOIN dbo.catalogo_delito cd ON cd.id_delito = sd.id_delito
        INNER JOIN dbo.catalogo_bien_juridico bj ON bj.id_bien_juridico = cd.id_bien_juridico
        INNER JOIN dbo.catalogo_entidad_federativa ef ON ef.id_entidad_federativa = d.id_entidad_federativa
        INNER JOIN dbo.catalogo_municipio mun ON mun.id_municipio = d.id_municipio
        LEFT JOIN dbo.catalogo_codigo_postal cp ON cp.id_codigo_postal = d.id_codigo_postal
        LEFT JOIN sabanas sab ON sab.id_modalidad_delito = d.id_modalidad_delito
            AND sab.id_grado_consumacion = d.id_grado_consumacion
            AND sab.id_instrumento_comision = d.id_instrumento_comision
            AND sab.id_forma_accion = d.id_forma_accion
        INNER JOIN dbo.catalogo_tipo_victima tv ON tv.id_tipo_victima = v.id_tipo_victima
        LEFT JOIN dbo.catalogo_tipo_victima_moral tvm ON tvm.id_tipo_victima_moral = v.id_tipo_victima_moral
        LEFT JOIN dbo.catalogo_sexo sx ON sx.id_sexo = v.id_sexo
        LEFT JOIN dbo.catalogo_genero gen ON gen.id_genero = v.id_genero
        LEFT JOIN dbo.catalogo_pertenece_poblacion_indigena pob ON pob.id_pertenece_poblacion_indigena = v.id_pertenece_poblacion_indigena
        LEFT JOIN dbo.catalogo_presenta_discapacidad disc ON disc.id_presenta_discapacidad = v.id_presenta_discapacidad
        LEFT JOIN dbo.catalogo_nacionalidad nac ON nac.id_nacionalidad = v.id_nacionalidad
        LEFT JOIN dbo.catalogo_entidad_federativa ef_reg ON ef_reg.id_entidad_federativa = COALESCE(c_v.id_entidad_federativa, u_v.id_entidad_federativa)
        WHERE c_ci.anio_corte = @Anio AND c_ci.mes_corte = @Mes
          AND ci.activo = 1 AND d.activo = 1 AND v.activo = 1
          AND c_ci.activo = 1 AND c_d.activo = 1 AND c_v.activo = 1
          AND ((c_ci.tipo_carga = N'CARGA_INICIAL' AND c_ci.estado = N'CONFIRMADO')
            OR (c_ci.tipo_carga = N'ACTUALIZACION' AND c_ci.estado = N'CONFIRMADO_ACTUALIZACION'))
          AND ((c_d.tipo_carga = N'CARGA_INICIAL' AND c_d.estado = N'CONFIRMADO')
            OR (c_d.tipo_carga = N'ACTUALIZACION' AND c_d.estado = N'CONFIRMADO_ACTUALIZACION'))
          AND ((c_v.tipo_carga = N'CARGA_INICIAL' AND c_v.estado = N'CONFIRMADO')
            OR (c_v.tipo_carga = N'ACTUALIZACION' AND c_v.estado = N'CONFIRMADO_ACTUALIZACION'))
          AND sd.subtipo_delito = N'Feminicidio' AND gc.descripcion = N'Consumado'
          AND u_ci.usuario <> N'FGRX26011400' AND u_d.usuario <> N'FGRX26011400' AND u_v.usuario <> N'FGRX26011400'
        ORDER BY ef_reg.nombre, ci.identificador_carpeta_fiscalia,
            d.identificador_delito_fiscalia, v.identificador_victima_fiscalia, v.id_victima;
        """;
}

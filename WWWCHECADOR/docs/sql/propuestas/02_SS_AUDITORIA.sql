/* =====================================================================
   PROPUESTA 02 — Bitácora de auditoría del sitio administrativo
   ESTADO: NO EJECUTADO. Requiere autorización antes de aplicarse.
   Base: DB_CHECADOR_ENT

   Hoy el sitio audita en archivos JSONL (Auditoria:CarpetaArchivo) y en el log.
   Tras crear esta tabla, activar Auditoria:RegistrarEnBaseDatos = true.
   Las columnas coinciden con el INSERT de src/ChecadorWeb/Services/AuditoriaService.cs.

   Reglas:
   - Nunca se registran contraseñas, PIN ni hashes en TX_DETALLE.
   - Tabla de solo inserción: se recomienda que la cuenta del sitio tenga
     únicamente INSERT/SELECT sobre ella (sin UPDATE/DELETE).
   ===================================================================== */

CREATE TABLE dbo.SS_AUDITORIA
(
    ID_AUDITORIA  bigint IDENTITY(1,1) NOT NULL,
    FE_EVENTO     datetimeoffset(3) NOT NULL CONSTRAINT DF_SS_AUDITORIA_FE DEFAULT (SYSDATETIMEOFFSET()),
    ID_USUARIO    varchar(25)    NULL,      -- usuario autenticado (NULL en intentos de login fallidos)
    NB_ACCION     varchar(40)    NOT NULL,  -- LOGIN, LOGOUT, ALTA, EDICION, ACTIVACION, DESACTIVACION,
                                            -- EDICION_HORARIO, VINCULAR, DESVINCULAR, RESTABLECER_PASSWORD,
                                            -- CAMBIO_PASSWORD_PROPIO, VER_FOTO
    NB_ENTIDAD    varchar(60)    NOT NULL,  -- tabla afectada (MM_TRABAJADORES, SS_USUARIOS, ...)
    TX_CLAVE      nvarchar(100)  NULL,      -- clave del registro afectado
    ST_EXITOSO    bit            NOT NULL,
    TX_DETALLE    nvarchar(1000) NULL,      -- resumen sin datos sensibles
    TX_IP         varchar(45)    NULL,      -- IPv4/IPv6
    TX_TRAZA      varchar(100)   NULL,      -- TraceIdentifier de ASP.NET Core para correlacionar con logs
    CONSTRAINT PK_SS_AUDITORIA PRIMARY KEY CLUSTERED (ID_AUDITORIA)
);

CREATE NONCLUSTERED INDEX IX_SS_AUDITORIA_FECHA ON dbo.SS_AUDITORIA (FE_EVENTO DESC) INCLUDE (ID_USUARIO, NB_ACCION, NB_ENTIDAD);
CREATE NONCLUSTERED INDEX IX_SS_AUDITORIA_ENTIDAD ON dbo.SS_AUDITORIA (NB_ENTIDAD, TX_CLAVE, FE_EVENTO DESC);
CREATE NONCLUSTERED INDEX IX_SS_AUDITORIA_USUARIO ON dbo.SS_AUDITORIA (ID_USUARIO, FE_EVENTO DESC);

-- Permisos sugeridos para la cuenta del sitio (ajustar el nombre del usuario de BD):
-- GRANT INSERT, SELECT ON dbo.SS_AUDITORIA TO [usuario_checadorweb];
-- DENY UPDATE, DELETE ON dbo.SS_AUDITORIA TO [usuario_checadorweb];

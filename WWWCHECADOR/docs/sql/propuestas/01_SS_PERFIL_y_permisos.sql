/* =====================================================================
   PROPUESTA 01 — Catálogo de perfiles y permisos por sección del sitio
   ESTADO: NO EJECUTADO. Requiere autorización antes de aplicarse.
   Base: DB_CHECADOR_ENT

   - dbo.SS_PERFIL usa exactamente la estructura definida por el negocio.
   - dbo.SS_PERFIL_PERMISO es una propuesta para mover a la BD la asignación
     perfil → permisos que hoy vive en appsettings (Seguridad:PermisosPorPerfil).
     El sitio aún NO lee esta tabla; leerla requiere un cambio aprobado.
   - El sitio ya lee dbo.SS_PERFIL (solo nombres) en cuanto exista.
   ===================================================================== */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE [dbo].[SS_PERFIL](
	[ID_PERFIL] [int] IDENTITY(1,1) NOT NULL,
	[NB_NOMBRE] [nvarchar](100) NOT NULL,
	[CRTD_DATETIME] [datetime2](7) NOT NULL,
	[CRTD_USER] [nvarchar](30) NOT NULL,
	[LUPD_DATETIME] [datetime2](7) NULL,
	[LUPD_USER] [nvarchar](30) NULL,
 CONSTRAINT [PK_SS_PERFIL] PRIMARY KEY CLUSTERED
(
	[ID_PERFIL] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

-- El perfil 1 ya se usa en SS_USUARIOS y en sp_desktop_login_admin (administrador).
SET IDENTITY_INSERT dbo.SS_PERFIL ON;
INSERT INTO dbo.SS_PERFIL (ID_PERFIL, NB_NOMBRE, CRTD_DATETIME, CRTD_USER)
VALUES (1, N'Administrador', SYSDATETIME(), N'script');
SET IDENTITY_INSERT dbo.SS_PERFIL OFF;

-- Integridad: todo usuario debe tener un perfil existente.
ALTER TABLE dbo.SS_USUARIOS WITH CHECK
    ADD CONSTRAINT FK_SS_USUARIOS_SS_PERFIL FOREIGN KEY (ID_PERFIL) REFERENCES dbo.SS_PERFIL (ID_PERFIL);

-- Permisos del sitio por perfil. Valores válidos de CD_PERMISO (ver src/ChecadorWeb/Security/Permisos.cs):
-- *, panel.ver, empleados.ver, empleados.editar, horarios.ver, horarios.editar,
-- equipos.ver, equipos.editar, usuarios.ver, usuarios.editar, estaciones.ver,
-- marcajes.ver, marcajes.foto
CREATE TABLE dbo.SS_PERFIL_PERMISO
(
    ID_PERFIL      int           NOT NULL,
    CD_PERMISO     varchar(40)   NOT NULL,
    CRTD_DATETIME  datetime2(7)  NOT NULL CONSTRAINT DF_SS_PERFIL_PERMISO_CRTD DEFAULT (SYSDATETIME()),
    CRTD_USER      nvarchar(30)  NOT NULL,
    CONSTRAINT PK_SS_PERFIL_PERMISO PRIMARY KEY CLUSTERED (ID_PERFIL, CD_PERMISO),
    CONSTRAINT FK_SS_PERFIL_PERMISO_SS_PERFIL FOREIGN KEY (ID_PERFIL) REFERENCES dbo.SS_PERFIL (ID_PERFIL),
    CONSTRAINT CK_SS_PERFIL_PERMISO_CODIGO CHECK (CD_PERMISO IN (
        '*', 'panel.ver', 'empleados.ver', 'empleados.editar', 'horarios.ver', 'horarios.editar',
        'equipos.ver', 'equipos.editar', 'usuarios.ver', 'usuarios.editar', 'estaciones.ver',
        'marcajes.ver', 'marcajes.foto'))
);

INSERT INTO dbo.SS_PERFIL_PERMISO (ID_PERFIL, CD_PERMISO, CRTD_USER) VALUES (1, '*', N'script');

COMMIT TRANSACTION;

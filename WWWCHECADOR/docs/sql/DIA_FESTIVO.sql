-- ============================================================
-- Script: Creación de la tabla MM_DIAS_FESTIVOS
-- Módulo: Días Festivos (CRUD administrativo)
-- Nomenclatura: MM_ = catálogos maestros
-- ============================================================

-- ------------------------------------------------------------
-- 1. Tabla principal
-- ------------------------------------------------------------
CREATE TABLE [dbo].[MM_DIAS_FESTIVOS]
(
    [ID]                INT          NOT NULL  IDENTITY(1,1),
    [Fecha]             DATE         NOT NULL,                   -- Solo fecha, sin hora
    [Descripcion]       VARCHAR(150) NOT NULL,
    [EsOficial]         BIT          NOT NULL  DEFAULT (1),      -- 1=Ley Federal, 0=Asueto Interno
    [Estatus]           BIT          NOT NULL  DEFAULT (1),      -- 1=Activo, 0=Baja lógica

    -- Auditoría
    [create_time]       DATETIME     NOT NULL  DEFAULT (GETDATE()),
    [Create_ID_USUARIO] VARCHAR(25)  NULL,
    [update_time]       DATETIME     NOT NULL  DEFAULT (GETDATE()),
    [Update_ID_USUARIO] VARCHAR(25)  NULL,

    CONSTRAINT [PK_MM_DIAS_FESTIVOS] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

-- ------------------------------------------------------------
-- 2. Índice único sobre Fecha (impide duplicados de fecha)
-- ------------------------------------------------------------
CREATE UNIQUE NONCLUSTERED INDEX [UQ_MM_DIAS_FESTIVOS_Fecha]
    ON [dbo].[MM_DIAS_FESTIVOS] ([Fecha] ASC);
GO

-- ------------------------------------------------------------
-- 3. Índice de cobertura para el motor de asistencias
--    (rango de fechas con filtro de Estatus activo)
-- ------------------------------------------------------------
CREATE NONCLUSTERED INDEX [IX_MM_DIAS_FESTIVOS_Estatus_Fecha]
    ON [dbo].[MM_DIAS_FESTIVOS] ([Estatus] ASC, [Fecha] ASC);
GO

PRINT 'Tabla MM_DIAS_FESTIVOS creada correctamente.';
GO

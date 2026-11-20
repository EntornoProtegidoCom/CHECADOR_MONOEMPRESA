SET NOCOUNT ON;
SELECT @@SERVERNAME AS Servidor,
    DB_NAME() AS BaseDatos,
    GETDATE() AS HoraServidor;
DECLARE @xml xml = '<root><accion>O</accion><IDTrabajador>1</IDTrabajador><TipoRegistro>1</TipoRegistro><Horaregistro>2026-11-02T14:28:31</Horaregistro><IPRegistro>127.0.0.1</IPRegistro><NBEquipo>LPENTORNO165</NBEquipo></root>';
SELECT r.value('(PIN/text())[1]', 'int') AS PINInterpretado,
    r.value('(HashBuscar/text())[1]', 'varchar(512)') AS HashBuscarInterpretado
FROM @xml.nodes('/root') AS T(r);
WITH Capturas AS (
    SELECT GuidRegistro,
        CONVERT(datetime2(3), Fecha, 126) AS FechaHoraCaptura,
        TipoRegistro
    FROM (
            VALUES (
                    '716bbdd3-2682-41de-a28d-bb6190573691',
                    '2026-11-02T14:28:31.247',
                    1
                ),
                (
                    '7d4c4b5d-c375-4a78-92f1-95fa2ae5d41c',
                    '2026-11-03T17:22:47.674',
                    1
                ),
                (
                    '44d94e4e-bfa3-4c5b-a62c-12e0406e678b',
                    '2026-11-03T17:28:56.203',
                    2
                ),
                (
                    '057b9903-57a0-4360-9fc7-052a79f62d50',
                    '2026-11-03T17:34:44.499',
                    3
                ),
                (
                    '1998d903-eaa5-49b9-a16b-89681976c6e3',
                    '2026-11-03T17:40:16.982',
                    4
                ),
                (
                    '0ce086b9-3f1b-4724-a554-aae2253c7829',
                    '2026-11-04T09:00:13.836',
                    1
                )
        ) AS V(GuidRegistro, Fecha, TipoRegistro)
),
Eventos AS (
    SELECT *,
        CONVERT(
            datetime,
            CONVERT(varchar(19), FechaHoraCaptura, 126),
            126
        ) AS HoraregistroXml
    FROM Capturas
),
Hashes AS (
    SELECT *,
        HASHBYTES(
            'SHA2_256',
            CONVERT(
                nvarchar(100),
                '1|' + CONVERT(varchar(30), HoraregistroXml, 126)
            )
        ) AS HashEsperado
    FROM Eventos
)
SELECT E.GuidRegistro,
    E.FechaHoraCaptura,
    E.HoraregistroXml,
    E.TipoRegistro,
    CONVERT(varchar(66), E.HashEsperado, 1) AS HashEsperado,
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.MM_TRABAJADORES
            WHERE IDTrabajador = 1
                AND ISNULL(status, 0) = 1
        ) THEN 1
        ELSE 0
    END AS TrabajadorActivo,
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.DD_EQUIPO_VINCULO
            WHERE IDTrabajador = 1
                AND NBEquipo = 'LPENTORNO165'
                AND Activo = 1
        ) THEN 1
        ELSE 0
    END AS VinculoActivo,
    (
        SELECT COUNT(*)
        FROM dbo.MM_HORARIO H
        WHERE H.IDTrabajador = 1
            AND H.Horaregistro >= E.HoraregistroXml
            AND H.Horaregistro < DATEADD(second, 1, E.HoraregistroXml)
    ) AS CoincidenciasHoraCualquierTipo,
    (
        SELECT COUNT(*)
        FROM dbo.MM_HORARIO H
        WHERE H.IDTrabajador = 1
            AND H.Horaregistro >= E.HoraregistroXml
            AND H.Horaregistro < DATEADD(second, 1, E.HoraregistroXml)
            AND H.TipoRegistro = E.TipoRegistro
    ) AS CoincidenciasHoraYTipo,
    (
        SELECT COUNT(*)
        FROM dbo.MM_HORARIO H
        WHERE H.IDTrabajador = 1
            AND H.HashRegistro = E.HashEsperado
    ) AS CoincidenciasHash,
    (
        SELECT COUNT(*)
        FROM dbo.MM_HORARIO H
        WHERE H.IDTrabajador = 1
            AND H.TipoRegistro = E.TipoRegistro
            AND H.Horaregistro >= CONVERT(date, E.HoraregistroXml)
            AND H.Horaregistro < DATEADD(
                day,
                1,
                CONVERT(datetime, CONVERT(date, E.HoraregistroXml))
            )
    ) AS CoincidenciasTipoDia,
    CASE
        WHEN E.HoraregistroXml > GETDATE() THEN 1
        ELSE 0
    END AS FechaFuturaAhora,
    (
        SELECT 'O' AS accion,
            1 AS IDTrabajador,
            E.TipoRegistro,
            CONVERT(varchar(19), E.HoraregistroXml, 126) AS Horaregistro,
            '127.0.0.1' AS IPRegistro,
            'LPENTORNO165' AS NBEquipo FOR XML PATH('root'),
            TYPE
    ) AS XmlReconstruido
FROM Hashes E
ORDER BY E.FechaHoraCaptura;
SELECT H.ID,
    H.IDTrabajador,
    H.Horaregistro,
    H.TipoRegistro,
    H.NBEquipo,
    CONVERT(varchar(66), H.HashRegistro, 1) AS HashRegistro
FROM dbo.MM_HORARIO H
WHERE H.IDTrabajador = 1
    AND H.Horaregistro >= '20261102'
    AND H.Horaregistro < '20261105'
ORDER BY H.Horaregistro,
    H.ID;
SELECT IDTrabajador,
    status
FROM dbo.MM_TRABAJADORES
WHERE IDTrabajador = 1;
SELECT ID,
    NBEquipo,
    IDTrabajador,
    Activo,
    create_time,
    update_time
FROM dbo.DD_EQUIPO_VINCULO
WHERE IDTrabajador = 1
    OR NBEquipo = 'LPENTORNO165';
SELECT modify_date,
    OBJECT_DEFINITION(object_id) AS DefinicionActual
FROM sys.procedures
WHERE object_id = OBJECT_ID('dbo.sp_registro_checador');
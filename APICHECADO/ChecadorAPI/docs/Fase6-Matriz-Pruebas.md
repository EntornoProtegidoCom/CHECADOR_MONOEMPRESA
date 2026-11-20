# Fase 6 - Matriz de Pruebas NeedPin

## Objetivo

Validar el comportamiento funcional y de seguridad de los endpoints de marcaje en dos modos:

1. NeedPin = true
2. NeedPin = false

La validacion cubre:

1. Flujo online (accion I)
2. Flujo offline (accion O)
3. Validacion por PIN
4. Validacion por vinculo activo de estacion
5. Validacion de trabajador activo
6. Reglas de negocio del SP (tipo de registro, duplicidad, ventana de 5 minutos, idempotencia offline)

## Precondiciones

1. API compilada y ejecutandose en http://localhost:8080.
2. Encabezado X-Api-Key configurado correctamente.
3. SP sp_registro_checador desplegado con logica de PIN opcional.
4. Existe al menos un trabajador activo con status = 1 en MM_TRABAJADORES.
5. Existe al menos un trabajador inactivo con status = 0 en MM_TRABAJADORES.
6. Existe al menos una estacion con vinculo activo en DD_EQUIPO_VINCULO para un trabajador activo.
7. Existe al menos una estacion sin vinculo activo para ese trabajador.

## Configuracion de ejecucion

### Escenario A: NeedPin = true

En appsettings de ambiente:

```json
{
  "ApiSettings": {
    "NeedPin": true
  }
}
```

### Escenario B: NeedPin = false

En appsettings de ambiente:

```json
{
  "ApiSettings": {
    "NeedPin": false
  }
}
```

## Payload base online

```json
{
  "guidRegistro": "11111111-1111-1111-1111-111111111111",
  "idTrabajador": 1001,
  "pin": 1234,
  "fechaHoraCaptura": "2026-11-02T08:00:00",
  "tipoRegistro": 1,
  "ipRegistro": "127.0.0.1",
  "nbEquipo": "PC-RECEPCION-01",
  "fotoBase64": null
}
```

## Payload base offline

```json
{
  "registros": [
    {
      "guidRegistro": "22222222-2222-2222-2222-222222222222",
      "idTrabajador": 1001,
      "pin": 1234,
      "fechaHoraCaptura": "2026-11-02T08:05:00",
      "tipoRegistro": 1,
      "ipRegistro": "127.0.0.1",
      "nbEquipo": "PC-RECEPCION-01",
      "fotoBase64": null
    }
  ]
}
```

## Matriz de casos

| ID     | NeedPin | Endpoint                             | Caso                               | Entrada clave                           | Resultado esperado          |
| ------ | ------- | ------------------------------------ | ---------------------------------- | --------------------------------------- | --------------------------- |
| F6-001 | true    | POST /api/asistencia/marcaje/online  | Marcaje online valido con PIN      | pin > 0, trabajador activo              | HTTP 200, Exito=1           |
| F6-002 | true    | POST /api/asistencia/marcaje/online  | PIN faltante                       | pin null o 0                            | HTTP 400, Exito=2           |
| F6-003 | true    | POST /api/asistencia/marcaje/online  | PIN incorrecto                     | pin invalido                            | HTTP 400, Exito=2           |
| F6-004 | true    | POST /api/asistencia/marcaje/online  | Trabajador inactivo                | status=0                                | HTTP 400, Exito=2           |
| F6-005 | false   | POST /api/asistencia/marcaje/online  | Marcaje sin PIN con vinculo activo | pin null/0, nbEquipo con vinculo activo | HTTP 200, Exito=1           |
| F6-006 | false   | POST /api/asistencia/marcaje/online  | Marcaje sin PIN sin vinculo        | pin null/0, nbEquipo sin vinculo activo | HTTP 400, Exito=2           |
| F6-007 | false   | POST /api/asistencia/marcaje/online  | Sin PIN y sin nbEquipo             | pin null/0, nbEquipo vacio              | HTTP 400, Exito=2           |
| F6-008 | false   | POST /api/asistencia/marcaje/online  | Trabajador inactivo sin PIN        | status=0                                | HTTP 400, Exito=2           |
| F6-009 | true    | POST /api/asistencia/marcaje/offline | Lote offline valido con PIN        | pin > 0, fechaHoraCaptura valida        | HTTP 200, data.exitosos > 0 |
| F6-010 | true    | POST /api/asistencia/marcaje/offline | Offline sin fecha                  | fechaHoraCaptura default                | HTTP 200, data.fallidos > 0 |
| F6-011 | false   | POST /api/asistencia/marcaje/offline | Offline sin PIN con vinculo activo | pin null/0, nbEquipo con vinculo        | HTTP 200, data.exitosos > 0 |
| F6-012 | false   | POST /api/asistencia/marcaje/offline | Offline sin PIN sin vinculo        | pin null/0, nbEquipo sin vinculo        | HTTP 200, data.fallidos > 0 |
| F6-013 | false   | POST /api/asistencia/marcaje/offline | Offline futuro                     | fechaHoraCaptura > now                  | HTTP 200, data.fallidos > 0 |
| F6-014 | false   | POST /api/asistencia/marcaje/offline | Idempotencia hash                  | repetir mismo registro offline          | HTTP 200, data.fallidos > 0 |
| F6-015 | any     | POST /api/asistencia/marcaje/online  | TipoRegistro fuera de rango        | tipoRegistro 0 o 5                      | HTTP 400, Exito=2           |
| F6-016 | any     | POST /api/asistencia/marcaje/online  | FotoBase64 invalida                | cadena no base64                        | HTTP 400, Exito=2           |

## Comandos de prueba rapida

### Online

```bash
curl -X POST "http://localhost:8080/api/asistencia/marcaje/online" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TU_API_KEY" \
  -d "{\"guidRegistro\":\"11111111-1111-1111-1111-111111111111\",\"idTrabajador\":1001,\"pin\":1234,\"fechaHoraCaptura\":\"2026-11-02T08:00:00\",\"tipoRegistro\":1,\"ipRegistro\":\"127.0.0.1\",\"nbEquipo\":\"PC-RECEPCION-01\",\"fotoBase64\":null}"
```

### Offline

```bash
curl -X POST "http://localhost:8080/api/asistencia/marcaje/offline" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TU_API_KEY" \
  -d "{\"registros\":[{\"guidRegistro\":\"22222222-2222-2222-2222-222222222222\",\"idTrabajador\":1001,\"pin\":1234,\"fechaHoraCaptura\":\"2026-11-02T08:05:00\",\"tipoRegistro\":1,\"ipRegistro\":\"127.0.0.1\",\"nbEquipo\":\"PC-RECEPCION-01\",\"fotoBase64\":null}]}"
```

## Checklist de validacion final

1. Con NeedPin=true la API exige PIN en online y offline.
2. Con NeedPin=false la API acepta PIN nulo y delega validacion de vinculo al SP.
3. No hubo cambios de ruta ni contrato HTTP en endpoints existentes.
4. El log registra modo con-pin/sin-pin y contexto del marcaje sin exponer datos sensibles.
5. Todas las pruebas criticas de la matriz quedan evidenciadas (captura de request/response).

## Evidencias recomendadas

1. Captura de logs por cada caso critico (F6-002, F6-006, F6-011, F6-012).
2. Captura de respuesta JSON por cada caso.
3. Snapshot de filas insertadas en MM_HORARIO para casos exitosos.
4. Snapshot de DD_EQUIPO_VINCULO para casos con validacion de vinculo.

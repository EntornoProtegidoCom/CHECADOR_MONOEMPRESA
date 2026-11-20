# Diagnostico de lote offline: 2026-11-04

## Resultado actual: prueba completa autorizada

El usuario autorizo expresamente modificar DB_CHECADOR_ENT, al tratarse de una
base de pruebas respaldada. Esta fase posterior sustituye la restriccion de solo
lectura para SQL; SQLite y EstadoSync se mantuvieron sin cambios.

Antes de enviar se verificaron los seis eventos: cero coincidencias por hora,
hash o tipo diario. Se leyeron los datos y fotos originales de SQLite con
sqlite3 -readonly y se envio un unico lote a localhost:8080, con Pin=0.
Se conservaron los GUID y las fechas con milisegundos en el JSON; el SP recibio
las fechas sin milisegundos por el formato actual del servicio.

Respuesta real HTTP 200:

```json
{
  "exito": 1,
  "mensaje": "Lote procesado: 6 exitosos, 0 fallidos.",
  "data": {
    "totalProcesados": 6,
    "exitosos": 6,
    "fallidos": 0,
    "mensajes": [
      "GUID 716bbdd3-2682-41de-a28d-bb6190573691: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO",
      "GUID 7d4c4b5d-c375-4a78-92f1-95fa2ae5d41c: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO",
      "GUID 44d94e4e-bfa3-4c5b-a62c-12e0406e678b: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO",
      "GUID 057b9903-57a0-4360-9fc7-052a79f62d50: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO",
      "GUID 1998d903-eaa5-49b9-a16b-89681976c6e3: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO",
      "GUID 0ce086b9-3f1b-4724-a554-aae2253c7829: SE GRAB\u00d3 SU REGISTRO OFFLINE CON \u00c9XITO"
    ]
  }
}
```

Los acentos se representan con escapes JSON para conservar el texto original.
Segun el flujo del servicio, cada resultado exitoso corresponde a EXITO=1 del SP.

Verificacion posterior real en MM_HORARIO:

| GuidRegistro                         | ID SQL | Fecha SQL           | Tipo | Bytes foto |
| ------------------------------------ | ------ | ------------------- | ---- | ---------- |
| 716bbdd3-2682-41de-a28d-bb6190573691 | 19     | 2026-11-02 14:28:31 | 1    | 64407      |
| 7d4c4b5d-c375-4a78-92f1-95fa2ae5d41c | 20     | 2026-11-03 17:22:47 | 1    | 55924      |
| 44d94e4e-bfa3-4c5b-a62c-12e0406e678b | 21     | 2026-11-03 17:28:56 | 2    | 53834      |
| 057b9903-57a0-4360-9fc7-052a79f62d50 | 22     | 2026-11-03 17:34:44 | 3    | 64564      |
| 1998d903-eaa5-49b9-a16b-89681976c6e3 | 23     | 2026-11-03 17:40:16 | 4    | 53235      |
| 0ce086b9-3f1b-4724-a554-aae2253c7829 | 24     | 2026-11-04 09:00:13 | 1    | 68863      |

Cada evento tiene exactamente una coincidencia por fecha/tipo y una por hash.
Los bytes de foto coinciden con los originales. Se conservaron las seis filas
insertadas para que los marcajes del incidente queden disponibles en esta DB.

Se repitio solo el primer evento confirmado, para probar idempotencia. Respuesta
real HTTP 200:

```json
{
  "exito": 1,
  "mensaje": "Lote procesado: 0 exitosos, 1 fallidos.",
  "data": {
    "totalProcesados": 1,
    "exitosos": 0,
    "fallidos": 1,
    "mensajes": [
      "GUID 716bbdd3-2682-41de-a28d-bb6190573691 [Error]: EL REGISTRO OFFLINE YA FUE SINCRONIZADO PREVIAMENTE."
    ]
  }
}
```

Ese rechazo corresponde a la rama EXITO=2 del SP. El conteo final sigue en seis:
la repeticion no creo duplicados. Este mensaje actual de idempotencia no explica
los rechazos iniciales, cuando esos registros todavia no existian.

SQLite conserva EstadoSync=2, IntentosSync=1 y las fechas de sincronizacion
04/11 10:19:41.606 a .733. No se actualizaron registros ni bitacoras locales.

Se reviso tambien la configuracion junto al ejecutable activo de Desktop:
BaseUrl=http://app-entornoprotegido.dyndns.org:56657/. Una sonda con fecha y foto
invalidas a esa URL devolvio el mismo rechazo de fecha que localhost, no un rechazo
por PIN. Esto no demuestra una instancia SQL distinta ni confirma identidad de
backend; solo documenta que la validacion observable coincide actualmente.

Conclusion actual: el rechazo de los seis eventos no se reproduce con la API,
SP, configuracion y datos actuales. Si funcionan con fotos originales y Pin=0,
no se puede atribuir el incidente historico a esos valores sin logs originales.
El error comprobado que permanece es la interpretacion del sobre y el marcado
indiscriminado de EstadoSync por el cliente. No se cambio ese contrato ni el cliente.

## Alcance y seguridad de la fase inicial

No se reenviaron los seis marcajes reales ni se ejecuto el SP con acciones de escritura.
Se ejecuto un POST sintetico con fecha y foto invalidas que se rechazo en la
validacion de la API antes de abrir SQL.
No se modificaron MM_HORARIO, el procedimiento, la cola SQLite ni EstadoSync.
SQLite se consulto con sqlite3 -readonly, sin instanciar SQLiteRepository.
Los unicos cambios de codigo son pruebas sinteticas; no se cambio el contrato en produccion.

## Resultado y limite de la fase inicial

La causa del sobre Exito=1 y del falso EstadoSync=2 esta confirmada.
La causa EXITO/MENSAJE de cada rechazo historico no se recupero: la bitacora
SQLite solo almacena contadores, no Data.Mensajes. Las seis prevalidaciones
del SP local se evaluaron sin INSERT y no reprodujeron el rechazo actual.
No atribuir esos rechazos a PIN, tipo, vinculo, fecha, hash o duplicidad sin el mensaje original.

El usuario confirmo que las pruebas del incidente son locales: ChecadorDesktop
utiliza la API de la terminal y SQL LPENTORNO165\SQLEXPRESS2019 / DB_CHECADOR_ENT.
Las observaciones anteriores de archivos publicados o URLs remotas no identificaban
la instancia utilizada; se retira esa hipotesis para este incidente.
Se verifico respuesta HTTP 200 de la API activa en localhost:8080.

## Evidencia del cliente

Base consultada: ChecadorDesktop/bin/Debug/net8.0-windows/ChecadorLocal.db,
dentro de la carpeta hermana CLIENTCHECADO.

| GuidRegistro                         | FechaHoraCaptura local  | Tipo | IdLocal |
| ------------------------------------ | ----------------------- | ---- | ------- |
| 716bbdd3-2682-41de-a28d-bb6190573691 | 2026-11-02 14:28:31.247 | 1    | 57      |
| 7d4c4b5d-c375-4a78-92f1-95fa2ae5d41c | 2026-11-03 17:22:47.674 | 1    | 58      |
| 44d94e4e-bfa3-4c5b-a62c-12e0406e678b | 2026-11-03 17:28:56.203 | 2    | 59      |
| 057b9903-57a0-4360-9fc7-052a79f62d50 | 2026-11-03 17:34:44.499 | 3    | 60      |
| 1998d903-eaa5-49b9-a16b-89681976c6e3 | 2026-11-03 17:40:16.982 | 4    | 61      |
| 0ce086b9-3f1b-4724-a554-aae2253c7829 | 2026-11-04 09:00:13.836 | 1    | 62      |

Los seis tienen IDTrabajador=1, NBEquipo=LPENTORNO165, IPRegistro=127.0.0.1,
EstadoSync=2, IntentosSync=1 y UltimoError=NULL. Sus fotos tienen entre 53235 y
68863 bytes; no se valido aqui el contenido fotografico ni se envio a SQL.
FechaHoraSincronizacion: 2026-11-04 10:19:41.606 a 10:19:41.733.

BITACORA_LOCAL, Origen=SyncManager, Nivel=INFO:

| Id  | FechaHora               | Mensaje                                                           |
| --- | ----------------------- | ----------------------------------------------------------------- |
| 561 | 2026-11-04 10:15:05.391 | Lote de 6 marcajes sincronizado con exito. Exitosos=0, Fallidos=6 |
| 562 | 2026-11-04 10:18:08.989 | Lote de 6 marcajes sincronizado con exito. Exitosos=0, Fallidos=6 |
| 563 | 2026-11-04 10:19:41.758 | Lote de 6 marcajes sincronizado con exito. Exitosos=0, Fallidos=6 |

Esos textos no contienen los seis MENSAJE originales. Los logs individuales de
API incluyen fecha y tipo pero no GUID. La respuesta Data.Mensajes si incluye GUID;
el cliente no la persiste en el flujo actual.

## Flujo y XML

ProcesarLoteOfflineAsync ordena por FechaHoraCaptura. Para cada resultado:
Exito=1 incrementa Exitosos; cualquier otro resultado incrementa Fallidos.
Al terminar siempre usa ApiResponseDto.Ok. El controlador siempre usa Ok(result).
Por eso HTTP 200 / Exito=1 significa lote procesado, no seis inserciones confirmadas.

Ejemplo reconstruido del XML para el primer GUID, a partir del servicio fuente
y los datos locales; no es una captura historica del request:

```xml
<root><accion>O</accion><IDTrabajador>1</IDTrabajador><TipoRegistro>1</TipoRegistro><Horaregistro>2026-11-02T14:28:31</Horaregistro><IPRegistro>127.0.0.1</IPRegistro><NBEquipo>LPENTORNO165</NBEquipo></root>
```

Los restantes XML cambian TipoRegistro y Horaregistro segun la tabla, sin milisegundos.
GuidRegistro y HashLocal no se envian al SP. FotoBase64 se decodifica y se envia
por separado en @img_data. No se envia HashBuscar.

Pin=0 produce pinForPayload=NULL; XmlPayloadBuilder omite el elemento PIN.
Un SELECT aislado con la misma expresion XML del SP confirmo @PIN=NULL
y @HashBuscar=NULL, no cero ni cadena vacia. El SP permite PIN NULL/0 si
el trabajador esta activo y tiene vinculo activo con esa estacion.
NeedPin=false permite llegar a ese flujo; no omite la autorizacion del SP.

La regla de cinco minutos solo aparece en accion I del SP proporcionado.
La accion O no la aplica. Los cuatro eventos del 03/11 ademas estan separados
369, 348 y 332 segundos. No hay evidencia para culpar a esa regla.
La prueba TipoRegistro=0 es independiente y no corresponde a estos eventos.

Si el SP devuelve EXITO=0, el servicio actual lo convierte a Warning (Exito=2),
perdiendo la distincion entre fallo tecnico y rechazo de negocio.
Excepciones de conexion/SqlDataAdapter no se capturan por elemento y pueden
abortar el lote. Por tanto, 0/6 no identifica por si solo un fallo de conectividad.

## Verificacion SQL local anterior al envio autorizado

En LPENTORNO165\SQLEXPRESS2019 / DB_CHECADOR_ENT:

- Trabajador 1: status=1.
- Vinculo ID=5: LPENTORNO165, trabajador 1, Activo=1; creado 02/11 13:49:16.330.
- Ninguno de los seis eventos aparece en el segundo y tipo correspondientes.
- La consulta de MM_HORARIO para ese trabajador del 02/11 al 04/11 no devolvio filas.
- Esto describe el estado actual de la base local confirmada por el usuario.

El script [Diagnostico-Lote-Offline-SoloLectura.sql](Diagnostico-Lote-Offline-SoloLectura.sql)
comprueba hora, tipo, hash y duplicados diarios, genera XML y obtiene la definicion
actual del SP sin ejecutarlo. Los resultados son verificaciones de estado actual,
no respuestas historicas EXITO/MENSAJE ni pruebas de que una futura insercion pasaria.

Se confirmo ausencia actual de esos eventos en la base local. Antes de proponer
reenvio aun hace falta explicar el rechazo historico con logs de API o Data.Mensajes
del 04/11 10:15-10:20, o reproducir el recorrido completo en un entorno aislado aprobado.
No cambiar EstadoSync ni reenviar con los datos actuales disponibles.

## Prueba sin insercion posterior a la aclaracion

La API activa recibio solo una sonda sintetica, GUID
diagnostico-no-insertar-a9294bcd-434c-4d49-b2c3-7b705b2a0458,
con Pin=0, TipoRegistro=1, FechaHoraCaptura=0001-01-01 y foto Base64 invalida.
Respondio HTTP 200, exito=1, exitosos=0, fallidos=1 y:

```text
GUID diagnostico-no-insertar-a9294bcd-434c-4d49-b2c3-7b705b2a0458 [Error]: FechaHoraCaptura es obligatoria para el flujo offline.
```

La instancia activa acepta omitir PIN, pero no una fecha ausente. La sonda no
representa ninguno de los seis eventos y no alcanza la conexion SQL.

Tambien se reprodujo la conversion NText -> XML usando @param NText, como la API:
accion=O, trabajador=1, tipo=1, fecha valida, PIN=NULL y HashBuscar=NULL.

Se leyo la definicion local del SP y se evaluaron en memoria sus declaraciones,
extraccion XML y prevalidaciones de la accion O. Se excluyo todo el bloque de
INSERT y se bloqueo la ejecucion si el texto contenia instrucciones de escritura.
No se modifico el procedimiento ni se ejecuto su cuerpo completo.

| GuidRegistro                         | Resultado de prevalidacion             |
| ------------------------------------ | -------------------------------------- |
| 716bbdd3-2682-41de-a28d-bb6190573691 | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |
| 7d4c4b5d-c375-4a78-92f1-95fa2ae5d41c | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |
| 44d94e4e-bfa3-4c5b-a62c-12e0406e678b | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |
| 057b9903-57a0-4360-9fc7-052a79f62d50 | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |
| 1998d903-eaa5-49b9-a16b-89681976c6e3 | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |
| 0ce086b9-3f1b-4724-a554-aae2253c7829 | PREVALIDACIONES_SUPERADAS_SIN_INSERTAR |

Ese marcador es diagnostico, con EXITO=NULL; no es una respuesta original del SP
ni una confirmacion de insercion. El estado actual no reprodujo rechazos por
trabajador, vinculo, tipo, fecha, idempotencia o duplicado diario. No permite excluir
condiciones historicas diferentes ni fallas posteriores dentro del INSERT.

Se inspecciono el esquema: sin triggers ni check constraints en MM_HORARIO;
NuIntentos admite NULL y tiene default 0; la unica clave unica es el ID identity;
existe FK hacia MM_TRABAJADORES. La cuenta SQL de esta sesion tiene permiso INSERT,
sin que esto pruebe la identidad SQL historica del proceso de API.

Comprobacion final: cero filas para trabajador 1 entre el 02/11 y el 04/11.
El recorrido completo con foto e INSERT permanece sin ejecutar por la restriccion
de no insertar. Para observar ese recorrido sin modificar la base actual se necesita
una copia aislada aprobada o los logs originales del rechazo.

## Contrato recomendado, no implementado

Conservar HTTP 200 para un lote cuyo procesamiento termino y distinguir
Procesado de Sincronizado. Agregar resultados estructurados por GUID en lugar
de exigir al cliente interpretar cadenas de Data.Mensajes:

```json
{
  "exito": 1,
  "mensaje": "Lote procesado; revisar resultado individual",
  "data": {
    "estadoLote": "parcial",
    "totalProcesados": 2,
    "exitosos": 1,
    "fallidos": 1,
    "resultados": [
      {
        "guidRegistro": "guid-sintetico-1",
        "tipoRegistro": 1,
        "fechaHoraCaptura": "2026-01-01T08:00:00",
        "exito": 1,
        "codigo": "INSERTADO",
        "mensaje": "Marcaje confirmado",
        "reintentable": false
      },
      {
        "guidRegistro": "guid-sintetico-2",
        "tipoRegistro": 2,
        "fechaHoraCaptura": "2026-01-01T13:00:00",
        "exito": 2,
        "codigo": "VINCULO_INACTIVO",
        "mensaje": "Sin vinculo activo",
        "reintentable": false
      }
    ]
  }
}
```

estadoLote: completo, parcial o fallido; el sobre solo describe procesamiento.
Preservar exito=0 por elemento para errores tecnicos, exito=2 para negocio.
Mantener contadores y mensajes existentes para compatibilidad; coordinar el
cambio con desktop o versionarlo. Ningun cambio del sobre por si solo arregla
el cliente si sigue marcando todo EstadoSync=2.

El cliente debe confirmar exclusivamente cada GUID con exito individual 1 y
mantener errores por registro. Un duplicado solo puede tratarse como confirmado
despues de verificar que corresponde al mismo evento persistido, no por el texto
generico de un error. No reintentar automaticamente reglas definitivas ni
considerar cualquier 5xx reintentable sin asegurar idempotencia.

Registrar BatchId, GUID, tipo, fecha, EXITO original y MENSAJE en servidor y cliente;
no registrar PIN ni foto. El GUID debe vincularse a una identidad persistida si
se requiere idempotencia por GUID; actualmente el SP no lo recibe y calcula hash
por trabajador y fecha usando @Base NVARCHAR(100).

## Pruebas ejecutadas

OfflineBatchContractTests: 5 pruebas aprobadas, sin SQL real ni requests de insercion.

- Tres escenarios de controlador con IAsistenciaService simulado: 3/0, 2/1 y 0/3.
- Servicio real con tres entradas sinteticas invalidas: Exito=1, 0 exitosos, 3 fallidos;
  verifica que no se abre una conexion SQL y que los mensajes contienen GUID.
- XmlPayloadBuilder: PIN nulo omitido; accion O; sin hash ni GUID inventados.

Las pruebas de controlador no prueban insercion ni autorizacion del SP.
Las pruebas SQL end-to-end con casos exitosos y parciales requieren una base
aislada aprobada, no ejecutar los seis marcajes del incidente.

```powershell
dotnet test .\ChecadorAPI.Tests\ChecadorAPI.Tests.csproj --artifacts-path "$env:TEMP\ChecadorOfflineDiagnostics" --filter FullyQualifiedName~OfflineBatchContractTests
```

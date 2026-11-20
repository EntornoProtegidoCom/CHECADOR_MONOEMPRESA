# ChecadorWeb — Sitio administrativo del sistema Checador

Sitio ASP.NET Core **Razor Pages (.NET 8 LTS)** que administra empleados, horarios, equipos vinculados,
usuarios del sistema, estado de estaciones (heartbeat) y consulta de marcajes. Accede **directamente**
a `DB_CHECADOR_ENT` con **Entity Framework Core 8** (proveedor SQL Server). **No usa ChecadorAPI** ni su
API key; el navegador nunca se conecta a la base de datos.

## Estructura

```
ChecadorWeb.slnx
src/ChecadorWeb/
  Data/            DbContext y entidades generados (Database First) + transacciones
  Security/        autenticación con SS_USUARIOS, permisos por perfil, hash heredado
  Services/        lógica por módulo (reglas de los SP replicadas), auditoría
  Models/          modelos de vista (los formularios nunca enlazan entidades)
  Pages/           Razor Pages por módulo
  wwwroot/         Bootstrap local, site.css, site.js, estaciones.js (sin scripts en línea)
tests/ChecadorWeb.Tests/
  Unit/            hash, horarios, heartbeat, permisos, validaciones
  Web/             permisos/autorización/CSRF/cabeceras con WebApplicationFactory
  Integracion/     servicios contra la BD real en transacciones revertidas
docs/sql/propuestas/   cambios de esquema PROPUESTOS (no ejecutados)
```

## Esquema confirmado (Database First)

Generado con `dotnet ef dbcontext scaffold` solo para las tablas del alcance. Sin migraciones,
`EnsureCreated` ni cambios de esquema.

| Tabla                 | Clave                        | Notas relevantes                                                                                                              |
| --------------------- | ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `MM_TRABAJADORES`     | `IDTrabajador` (no identity) | `Contrasena varchar(10) NOT NULL`, `PIN int`, `status bit`, horarios `time(0)`, auditoría `Create/Update_ID_USUARIO` + fechas |
| `DD_EQUIPO_VINCULO`   | `ID` identity                | FK a `MM_TRABAJADORES`; `Activo bit`; `NBEquipo varchar(100)`                                                                 |
| `SS_USUARIOS`         | `ID_USUARIO varchar(25)`     | `ID_PERFIL int` (sin catálogo), `TX_PASSWORD varchar(64)`, `ST_VIGENTE varchar(1)`                                            |
| `DD_EQUIPO_HEARTBEAT` | `NBEquipo`                   | sin FK; `EstadoEstacion` lo envía la estación (siempre `ACTIVO`)                                                              |
| `MM_HORARIO`          | `ID` identity                | **marcajes** (no horarios); `TipoRegistro` 1–4; `Foto image`; `NBEquipo varchar(50)`                                          |

No existen triggers, restricciones CHECK ni índices adicionales; las reglas viven en procedimientos.

## Reglas de los procedimientos replicadas

EF Core no ejecuta los SP; el sitio reproduce sus reglas explícitamente:

| Origen                                            | Regla aplicada en el sitio                                                                                               |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `sp_desktop_login_admin`, `sp_seguridad_usuarios` | `TX_PASSWORD = HEX(SHA2_256(NVARCHAR))` en mayúsculas, `ST_VIGENTE = 'A'`; contraseña ≤ 50 caracteres                    |
| Alta web de `MM_TRABAJADORES`                     | `IDTrabajador` capturado por el usuario (asignado externamente); duplicados rechazados; `Contrasena = 'URGL' + PIN`; PIN de 4 dígitos |
| `sp_catalogo_trabajadores` (I), procedimiento legado | ID = `MAX + 1`; `Contrasena = 'URGL' + PIN`; PIN de 4 dígitos                                                              |
| `sp_catalogo_trabajadores` (U)                    | cambiar PIN no modifica `Contrasena`                                                                                     |
| `sp_desktop_vinculo_equipo`                       | un solo vínculo activo por `NBEquipo` (comparación sin distinguir mayúsculas); desvincular = `Activo = 0`                |
| `sp_registro_checador`                            | `NBEquipo` ≤ 50; marcajes agrupados por día calendario → sin cruces de medianoche (configurable); ≥ 5 min entre marcajes |
| `vw_Estaciones_Desconectadas_Turno`               | umbrales predeterminados de aviso 3 min y desconexión 15 min                                                             |
| `sp_ConsultaEnviosPendientes`                     | correos pendientes = `stEnviado = 0 AND NuIntentos < 2`                                                                  |

Reglas adicionales del sitio: no se desactiva un empleado con vínculos activos; no se vincula un
empleado inactivo; un segundo equipo para el mismo empleado requiere confirmación; no se puede
desactivar ni cambiar el perfil propio, ni dejar el sistema sin usuarios vigentes con `usuarios.editar`.
No hay borrados: solo activación/desactivación.

### Exportación de marcajes

En **Marcajes**, el botón **Descargar Excel** descarga un archivo `.xlsx` con todos los registros que coinciden con los
filtros de la última consulta aplicada (no solo la página visible), con el mismo orden y límite
de fechas de la tabla. Para cambiar el contenido, ajuste los filtros y pulse **Consultar** antes
de descargar. Requiere `marcajes.ver`; no incluye imágenes, solo su disponibilidad.
El archivo se genera con OpenXML (`DocumentFormat.OpenXml`); las celdas se guardan como texto,
sin interpretar su contenido como fórmulas.
El Excel conserva las columnas de la tabla: IdTrabajador, Empleado (nombre completo),
Fecha (dd/MM/yyyy), Hora (HH:mm:ss), Tipo, Equipo, Correo (Enviado o Pendiente (N intentos)) y Foto.
No se muestra ni se exporta la IP.
Si no hay coincidencias, contiene únicamente los encabezados.

### Resumen analítico de horas

En **Marcajes → Resumen de Horas** (`/ResumenH/ResumenHoras`), los usuarios con
`marcajes.ver` pueden consultar y descargar una matriz de horas de todos los empleados
actualmente activos, incluso sin registros. Permite filtrar por ID o términos del nombre
y por fechas inclusivas (máximo 366 días calendario); inicia con la semana actual,
de lunes a viernes. Solo se muestran y procesan los días de lunes a viernes del rango.
Sábados y domingos no generan columnas, horas ni omisiones, aunque tengan marcajes.
Si el rango contiene únicamente fin de semana, se solicita seleccionar otro rango.

- Secuencia diaria 1–2–3–4: salida menos entrada, descontando el tiempo de comida.
- Secuencia diaria 1–4: salida menos entrada, sin descuento de comida.
- Secuencias incompletas, duplicadas, desconocidas o con tiempos iguales/desordenados:
  `INCOMPLETO`, sin horas computables. No se combinan jornadas entre días.
- Días sin registros: `FALTA`, también considerados omisiones. Esta regla se aplica
  a los días de lunes a viernes seleccionados, incluidos festivos, otros descansos
  y fechas futuras; no existe un calendario de excepciones que permita distinguirlos.
- Las duraciones se muestran como horas:minutos:segundos, por día, por semana (lunes
  a viernes) y por periodo; por ejemplo, 1 hora, 43 minutos y 55 segundos se muestra
  como `01:43:55`. Pueden superar 24 horas. Se redondean al segundo solo al mostrar;
  las sumas se calculan antes del redondeo e incluyen únicamente jornadas válidas.
  Las semanas parciales incluyen únicamente las fechas consultadas; los totales
  con omisiones son parciales.

El botón **Descargar Excel** conserva los filtros de la consulta y genera un `.xlsx`
con ClosedXML: encabezados azul oscuro, jornadas de al menos 8 horas en verde,
jornadas menores a 8 horas en amarillo, omisiones en rojo y subtotales en gris.
Incluye columnas semanales y total del periodo, filtros y paneles inmovilizados.
Las duraciones son valores numéricos de Excel (fracciones de día) con formato
`[hh]:mm:ss`, por lo que se pueden sumar y sus horas no se reinician al pasar de 24.
No modifica marcajes ni exporta fotografías, credenciales o IP.

## Seguridad

- Cookie propia `__Host-ChecadorWeb` (HttpOnly, Secure, SameSite=Strict, 30 min deslizantes).
- **Revalidación en cada petición**: si el usuario se desactiva, cambia de perfil o de contraseña, su sesión se cierra.
- Autorización por políticas (`[Authorize(Policy = ...)]` por página y verificación explícita en handlers de escritura); política de respaldo exige autenticación.
- Antiforgery en todos los POST; CSP estricta (`script-src 'self'`, sin estilos/scripts en línea), `X-Frame-Options`, `nosniff`, HSTS fuera de desarrollo.
- Límite de intentos en login por IP (`Seguridad:IntentosLoginPorMinuto`) y mensaje genérico.
- PIN, contraseñas y hashes nunca se muestran ni se registran. Concurrencia optimista con `update_time`.
- Auditoría de cada operación con el usuario autenticado: log + archivo JSONL (`App_Data/auditoria`); tabla opcional (propuesta 02).

### Permisos por perfil

Mientras no exista `SS_PERFIL_PERMISO`, se configuran en `appsettings.json`:

```json
"Seguridad": {
  "PermisosPorPerfil": { "1": [ "*" ], "2": [ "estaciones.ver", "marcajes.ver" ] },
  "NombresPerfil":     { "1": "Administrador" }
}
```

Permisos: `panel.ver`, `empleados.ver|editar`, `horarios.ver|editar`, `equipos.ver|editar`,
`usuarios.ver|editar`, `estaciones.ver`, `marcajes.ver`, `marcajes.foto` (editar implica ver).
Un perfil sin permisos configurados no puede iniciar sesión. Cuando exista `dbo.SS_PERFIL` el sitio
toma los nombres de ahí automáticamente.

## Configuración y ejecución

La cadena de conexión **no** está en el repositorio:

```powershell
cd src\ChecadorWeb
dotnet user-secrets set "ConnectionStrings:ChecadorDb" "Server=SERVIDOR\INSTANCIA;Database=DB_CHECADOR_ENT;Trusted_Connection=True;TrustServerCertificate=True;Application Name=ChecadorWeb"
dotnet run --launch-profile https      # https://localhost:7015
```

En servidores use la variable de entorno `ConnectionStrings__ChecadorDb`. Se recomienda una cuenta de
BD dedicada con `SELECT` en las cinco tablas, `INSERT/UPDATE` en `MM_TRABAJADORES`, `DD_EQUIPO_VINCULO`
y `SS_USUARIOS`, y sin `DELETE`. En IIS/varias instancias, configure un almacén compartido de Data Protection.

Otras opciones (`appsettings.json`): `Heartbeat` (umbrales y refresco), `Horarios:PermitirCruceMedianoche`
(por defecto `false`), `Auditoria`, `BaseDatos:NivelCompatibilidad` (150).

## Pruebas

```powershell
dotnet test
```

- Unitarias y de autorización: no requieren BD.
- Integración: usan la cadena de user-secrets (o `CHECADOR_TEST_DB`) y se omiten si no hay conexión.
  Cada prueba corre en una **transacción que siempre se revierte** (único efecto: huecos en identity de
  `DD_EQUIPO_VINCULO`) y verifica compatibilidad con `sp_desktop_login_trabajador`, `sp_desktop_login_admin`,
  `sp_seguridad_usuarios`, `sp_desktop_vinculo_equipo` y `sp_desktop_registrar_heartbeat`.

## Hallazgos en el sistema existente (no modificados)

1. **`sp_desktop_login_admin` imprime la contraseña en texto plano** (`PRINT '@Password: ' ...`). Recomendación: eliminar esas líneas.
2. `ChecadorAPI/appsettings.json` y su README contienen la API key en claro.
3. `SS_USUARIOS.TX_PASSWORD` es SHA-256 sin sal; `MM_TRABAJADORES.Contrasena` deriva del PIN en claro. Requiere un plan de migración aprobado (p. ej. PBKDF2/Argon2 con verificación dual y cambio de los SP).
4. `sp_desktop_login_trabajador` trata `status NULL` como activo y `sp_registro_checador` como inactivo.
5. `sp_ConsultaEnviosPendientes` etiqueta los tipos 2 y 3 como `SALIDA` y no contempla el 4.
6. `sp_catalogo_trabajadores` calcula `MAX + 1` sin bloqueo (posibles IDs duplicados en altas simultáneas).
7. `DD_EQUIPO_VINCULO.NBEquipo` admite 100 caracteres, pero `MM_HORARIO`/`sp_registro_checador` solo 50.
8. Existen PIN duplicados entre empleados (válido porque el login es ID + PIN).

## Cambios de esquema propuestos (sin ejecutar)

- [01_SS_PERFIL_y_permisos.sql](docs/sql/propuestas/01_SS_PERFIL_y_permisos.sql): `SS_PERFIL` (estructura del negocio), FK desde `SS_USUARIOS` y `SS_PERFIL_PERMISO`.
- [02_SS_AUDITORIA.sql](docs/sql/propuestas/02_SS_AUDITORIA.sql): bitácora en BD; luego activar `Auditoria:RegistrarEnBaseDatos`.
- Persistencia de alertas de estaciones: no se implementó; las alertas se calculan en línea con umbrales configurables.
  Reviso el proyecto para darte instrucciones exactas (framework, cadena de conexión, autenticación).

Read
Completed with input: {
"path": "c:\\Proyectos\\CHECADOR_ENT\\WWWCHECADOR\\README.md"
}

Read
Completed with input: {
"path": "c:\\Proyectos\\CHECADOR_ENT\\WWWCHECADOR\\src\\ChecadorWeb\\ChecadorWeb.csproj"
}

Read
Completed with input: {
"path": "c:\\Proyectos\\CHECADOR_ENT\\WWWCHECADOR\\src\\ChecadorWeb\\appsettings.json"
}

Read
Completed with input: {
"path": "c:\\Proyectos\\CHECADOR_ENT\\WWWCHECADOR\\src\\ChecadorWeb\\Program.cs"
}

Search for `SS_PERFIL|OBJECT_ID|sys\.|FromSql|SqlQuery|ExecuteSql`

Search for `DbSet|ToTable`

Search for `FROM dbo\.|INSERT INTO`

Read
Completed with input: {
"path": "c:\\Proyectos\\CHECADOR_ENT\\WWWCHECADOR\\src\\ChecadorWeb\\appsettings.Development.json"
}

Revisé el proyecto: es **ASP.NET Core 8 (Razor Pages)**. La app lee la cadena de conexión `ConnectionStrings__ChecadorDb` y **no arranca** si falta. Usa cookies `__Host-`, así que **solo funciona por HTTPS**. Además escribe la auditoría en `App_Data\auditoria`. Los pasos de abajo toman esto en cuenta.

---

## 1. Publicar (en tu equipo de desarrollo)

```powershell
cd C:\Proyectos\CHECADOR_ENT\WWWCHECADOR
dotnet test
dotnet publish src\ChecadorWeb\ChecadorWeb.csproj -c Release -o .\publish\ChecadorWeb
Compress-Archive -Path .\publish\ChecadorWeb\* -DestinationPath .\publish\ChecadorWeb.zip -Force
```

Esto genera una publicación que depende del runtime de .NET instalado en el servidor (paso 2). Copia `ChecadorWeb.zip` al servidor.

---

## 2. Preparar Windows Server 2019 (PowerShell como administrador)

```powershell
Install-WindowsFeature Web-Server, Web-Default-Doc, Web-Static-Content, Web-Http-Errors, Web-Http-Logging, Web-Stat-Compression, Web-Filtering, Web-Mgmt-Console
```

Instala el **ASP.NET Core 8.0 Runtime – Windows Hosting Bundle** (https://dotnet.microsoft.com/download/dotnet/8.0) y después ejecuta:

```powershell
net stop was /y; net start w3svc
dotnet --list-runtimes     # debe aparecer Microsoft.AspNetCore.App 8.0.x
```

---

## 3. Crear el sitio en IIS

```powershell
Import-Module WebAdministration
$ruta = 'C:\inetpub\ChecadorWeb'
New-Item $ruta, "$ruta\App_Data\auditoria" -ItemType Directory -Force
Expand-Archive C:\Temp\ChecadorWeb.zip -DestinationPath $ruta -Force

# Application pool sin código administrado, con perfil de usuario cargado (guarda las llaves de Data Protection)
New-WebAppPool ChecadorWeb
Set-ItemProperty IIS:\AppPools\ChecadorWeb managedRuntimeVersion ''
Set-ItemProperty IIS:\AppPools\ChecadorWeb processModel.loadUserProfile $true
Set-ItemProperty IIS:\AppPools\ChecadorWeb startMode AlwaysRunning

# Permisos: lectura en el sitio, escritura solo en App_Data
icacls $ruta /grant "IIS AppPool\ChecadorWeb:(OI)(CI)RX"
icacls "$ruta\App_Data" /grant "IIS AppPool\ChecadorWeb:(OI)(CI)M"

# Sitio con HTTPS (obligatorio)
New-Website -Name ChecadorWeb -PhysicalPath $ruta -ApplicationPool ChecadorWeb -Port 443 -Ssl -HostHeader 'checador.midominio.local'
```

Asigna el certificado: en **IIS Manager → Sitio → Bindings → https → SSL certificate**, elige un certificado emitido por tu CA interna o uno comercial. Si quieres, agrega también un binding HTTP en el puerto 80; la app redirige a HTTPS.

> Publícalo como **sitio propio**, no como subcarpeta de otro sitio, para que no compartan cookies.

---

## 4. Cadena de conexión en el servidor

Guárdala como variable de entorno **del application pool**. Así queda en `applicationHost.config`, no en `web.config`, y no se pierde al volver a publicar:

```powershell
$cs = 'Server=SQLSERVER\INSTANCIA;Database=DB_CHECADOR_ENT;Trusted_Connection=True;TrustServerCertificate=True;Application Name=ChecadorWeb'
& "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.applicationHost/applicationPools "/+[name='ChecadorWeb'].environmentVariables.[name='ConnectionStrings__ChecadorDb',value='$cs']" /commit:apphost
Restart-WebAppPool ChecadorWeb
```

Si la cambias después, borra la anterior con `/-[name='ChecadorWeb'].environmentVariables.[name='ConnectionStrings__ChecadorDb']` y vuelve a agregarla.

Variantes de `Server=`:

- Instancia con nombre: `SQLSERVER\INSTANCIA`. Requiere el servicio SQL Browser y el puerto UDP 1434.
- Puerto fijo (recomendado): `SQLSERVER,1433`.
- Con login de SQL en lugar de Windows: cambia `Trusted_Connection=True` por `User Id=checador_web;Password=...`.
- `TrustServerCertificate=True` solo hace falta si el SQL Server tiene un certificado autofirmado. Si tiene uno válido, quítalo.

---

## 5. Configurar la instancia de SQL Server

**a) En el servidor SQL**

1. Abre **SQL Server Configuration Manager**.
2. En **Protocols for INSTANCIA**, habilita **TCP/IP**. Lo ideal es fijar un puerto en _IPAll → TCP Port_, por ejemplo 1433, y dejar vacío _TCP Dynamic Ports_. Reinicia el servicio.
3. Abre ese puerto TCP en el firewall:
   ```powershell
   New-NetFirewallRule -DisplayName 'SQL ChecadorWeb' -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
   ```
   Si no fijaste el puerto, abre también UDP 1434 e inicia el servicio **SQL Server Browser**.

**b) Login y permisos mínimos** (ejecutar en SSMS). Son los que indica el README: lectura en las 5 tablas, escritura en 3 y **sin DELETE**.

```sql
USE [master];
-- Elige UNA opción según dónde esté SQL Server:
-- (1) SQL en el MISMO servidor que IIS:
CREATE LOGIN [IIS APPPOOL\ChecadorWeb] FROM WINDOWS;
-- (2) SQL en OTRO servidor del dominio (cuenta de equipo del servidor web):
-- CREATE LOGIN [MIDOMINIO\SRVWEB01$] FROM WINDOWS;
-- (3) Login de SQL:
-- CREATE LOGIN [checador_web] WITH PASSWORD = '<contraseña fuerte>', CHECK_POLICY = ON;
GO
USE [DB_CHECADOR_ENT];
CREATE USER [ChecadorWeb] FOR LOGIN [IIS APPPOOL\ChecadorWeb];   -- usa el login que creaste
GRANT SELECT ON dbo.MM_TRABAJADORES     TO [ChecadorWeb];
GRANT SELECT ON dbo.DD_EQUIPO_VINCULO   TO [ChecadorWeb];
GRANT SELECT ON dbo.SS_USUARIOS         TO [ChecadorWeb];
GRANT SELECT ON dbo.DD_EQUIPO_HEARTBEAT TO [ChecadorWeb];
GRANT SELECT ON dbo.MM_HORARIO          TO [ChecadorWeb];
GRANT INSERT, UPDATE ON dbo.MM_TRABAJADORES   TO [ChecadorWeb];
GRANT INSERT, UPDATE ON dbo.DD_EQUIPO_VINCULO TO [ChecadorWeb];
GRANT INSERT, UPDATE ON dbo.SS_USUARIOS       TO [ChecadorWeb];
-- Solo si aplicas las propuestas de docs/sql/propuestas:
-- GRANT SELECT ON dbo.SS_PERFIL TO [ChecadorWeb];
-- GRANT INSERT ON dbo.SS_AUDITORIA TO [ChecadorWeb];
```

Una alternativa a la opción (2) es correr el application pool con una cuenta de servicio de dominio, por ejemplo `MIDOMINIO\svc_checadorweb`, y crear el login para esa cuenta. Si lo haces, aplica también a esa cuenta los permisos de carpeta del paso 3.

---

## 6. Verificar

1. Abre `https://checador.midominio.local/Account/Login` e inicia sesión con un usuario de `SS_USUARIOS` cuyo perfil esté en `PermisosPorPerfil`.
2. Si aparece **HTTP 500.30**, revisa el _Visor de eventos → Application_. Lo más común es que falte la cadena de conexión o que el login no tenga acceso. Para más detalle, pon temporalmente `stdoutLogEnabled="true"` en el `web.config` publicado y crea la carpeta `logs`.

## 7. Actualizaciones posteriores

1. Copia un `app_offline.htm` a `C:\inetpub\ChecadorWeb` para detener la app.
2. Reemplaza los archivos, pero **no borres `App_Data`**.
3. Elimina `app_offline.htm`.

La cadena de conexión no se pierde porque está en el application pool.

## 8. Proyect tree

```
WWWCHECADOR
├─ ChecadorWeb.slnx
├─ docs
│  └─ sql
│     └─ propuestas
│        ├─ 01_SS_PERFIL_y_permisos.sql
│        └─ 02_SS_AUDITORIA.sql
├─ README.md
├─ src
│  └─ ChecadorWeb
│     ├─ appsettings.Development.json
│     ├─ appsettings.json
│     ├─ ChecadorWeb.csproj
│     ├─ Data
│     │  ├─ ChecadorDbContext.cs
│     │  ├─ Entities
│     │  │  ├─ DD_EQUIPO_HEARTBEAT.cs
│     │  │  ├─ DD_EQUIPO_VINCULO.cs
│     │  │  ├─ MM_HORARIO.cs
│     │  │  ├─ MM_TRABAJADORE.cs
│     │  │  └─ SS_USUARIO.cs
│     │  └─ TransaccionExtensions.cs
│     ├─ Infrastructure
│     │  ├─ CabecerasSeguridad.cs
│     │  └─ PaginaBase.cs
│     ├─ Models
│     │  ├─ Comunes.cs
│     │  ├─ Empleados.cs
│     │  ├─ PaginadorVista.cs
│     │  ├─ Usuarios.cs
│     │  └─ Vinculos.cs
│     ├─ Pages
│     │  ├─ Account
│     │  │  ├─ AccesoDenegado.cshtml
│     │  │  ├─ AccesoDenegado.cshtml.cs
│     │  │  ├─ Login.cshtml
│     │  │  ├─ Login.cshtml.cs
│     │  │  ├─ Logout.cshtml
│     │  │  ├─ Logout.cshtml.cs
│     │  │  ├─ MiCuenta.cshtml
│     │  │  └─ MiCuenta.cshtml.cs
│     │  ├─ Empleados
│     │  │  ├─ Crear.cshtml
│     │  │  ├─ Crear.cshtml.cs
│     │  │  ├─ Editar.cshtml
│     │  │  ├─ Editar.cshtml.cs
│     │  │  ├─ Index.cshtml
│     │  │  └─ Index.cshtml.cs
│     │  ├─ Equipos
│     │  │  ├─ Index.cshtml
│     │  │  ├─ Index.cshtml.cs
│     │  │  ├─ Vincular.cshtml
│     │  │  └─ Vincular.cshtml.cs
│     │  ├─ Error.cshtml
│     │  ├─ Error.cshtml.cs
│     │  ├─ Estaciones
│     │  │  ├─ Index.cshtml
│     │  │  └─ Index.cshtml.cs
│     │  ├─ Horarios
│     │  │  ├─ Editar.cshtml
│     │  │  ├─ Editar.cshtml.cs
│     │  │  ├─ Index.cshtml
│     │  │  └─ Index.cshtml.cs
│     │  ├─ Index.cshtml
│     │  ├─ Index.cshtml.cs
│     │  ├─ Marcajes
│     │  │  ├─ Index.cshtml
│     │  │  └─ Index.cshtml.cs
│     │  ├─ Shared
│     │  │  ├─ _CamposEmpleado.cshtml
│     │  │  ├─ _CamposHorario.cshtml
│     │  │  ├─ _CamposUsuario.cshtml
│     │  │  ├─ _ErrorCarga.cshtml
│     │  │  ├─ _EstadoVacio.cshtml
│     │  │  ├─ _Layout.cshtml
│     │  │  ├─ _Mensaje.cshtml
│     │  │  ├─ _Paginador.cshtml
│     │  │  └─ _ValidationScriptsPartial.cshtml
│     │  ├─ Usuarios
│     │  │  ├─ Crear.cshtml
│     │  │  ├─ Crear.cshtml.cs
│     │  │  ├─ Editar.cshtml
│     │  │  ├─ Editar.cshtml.cs
│     │  │  ├─ Index.cshtml
│     │  │  ├─ Index.cshtml.cs
│     │  │  ├─ Password.cshtml
│     │  │  └─ Password.cshtml.cs
│     │  ├─ _ViewImports.cshtml
│     │  └─ _ViewStart.cshtml
│     ├─ Program.cs
│     ├─ Properties
│     │  └─ launchSettings.json
│     ├─ Security
│     │  ├─ AutenticacionService.cs
│     │  ├─ LegacyPasswordHasher.cs
│     │  ├─ PerfilCatalogo.cs
│     │  ├─ PermisoAuthorization.cs
│     │  ├─ PermisoResolver.cs
│     │  ├─ Permisos.cs
│     │  └─ SeguridadOptions.cs
│     ├─ Services
│     │  ├─ AuditoriaService.cs
│     │  ├─ EmpleadoService.cs
│     │  ├─ EstacionYMarcajeServices.cs
│     │  ├─ HeartbeatClasificador.cs
│     │  ├─ HorarioValidator.cs
│     │  ├─ PanelService.cs
│     │  ├─ UsuarioService.cs
│     │  └─ VinculoService.cs
│     └─ wwwroot
│        ├─ css
│        │  └─ site.css
│        ├─ favicon.ico
│        ├─ js
│        │  ├─ estaciones.js
│        │  └─ site.js
│        └─ lib
│           ├─ bootstrap
│           │  ├─ dist
│           │  │  ├─ css
│           │  │  │  ├─ bootstrap-grid.css
│           │  │  │  ├─ bootstrap-grid.css.map
│           │  │  │  ├─ bootstrap-grid.min.css
│           │  │  │  ├─ bootstrap-grid.min.css.map
│           │  │  │  ├─ bootstrap-grid.rtl.css
│           │  │  │  ├─ bootstrap-grid.rtl.css.map
│           │  │  │  ├─ bootstrap-grid.rtl.min.css
│           │  │  │  ├─ bootstrap-grid.rtl.min.css.map
│           │  │  │  ├─ bootstrap-reboot.css
│           │  │  │  ├─ bootstrap-reboot.css.map
│           │  │  │  ├─ bootstrap-reboot.min.css
│           │  │  │  ├─ bootstrap-reboot.min.css.map
│           │  │  │  ├─ bootstrap-reboot.rtl.css
│           │  │  │  ├─ bootstrap-reboot.rtl.css.map
│           │  │  │  ├─ bootstrap-reboot.rtl.min.css
│           │  │  │  ├─ bootstrap-reboot.rtl.min.css.map
│           │  │  │  ├─ bootstrap-utilities.css
│           │  │  │  ├─ bootstrap-utilities.css.map
│           │  │  │  ├─ bootstrap-utilities.min.css
│           │  │  │  ├─ bootstrap-utilities.min.css.map
│           │  │  │  ├─ bootstrap-utilities.rtl.css
│           │  │  │  ├─ bootstrap-utilities.rtl.css.map
│           │  │  │  ├─ bootstrap-utilities.rtl.min.css
│           │  │  │  ├─ bootstrap-utilities.rtl.min.css.map
│           │  │  │  ├─ bootstrap.css
│           │  │  │  ├─ bootstrap.css.map
│           │  │  │  ├─ bootstrap.min.css
│           │  │  │  ├─ bootstrap.min.css.map
│           │  │  │  ├─ bootstrap.rtl.css
│           │  │  │  ├─ bootstrap.rtl.css.map
│           │  │  │  ├─ bootstrap.rtl.min.css
│           │  │  │  └─ bootstrap.rtl.min.css.map
│           │  │  └─ js
│           │  │     ├─ bootstrap.bundle.js
│           │  │     ├─ bootstrap.bundle.js.map
│           │  │     ├─ bootstrap.bundle.min.js
│           │  │     ├─ bootstrap.bundle.min.js.map
│           │  │     ├─ bootstrap.esm.js
│           │  │     ├─ bootstrap.esm.js.map
│           │  │     ├─ bootstrap.esm.min.js
│           │  │     ├─ bootstrap.esm.min.js.map
│           │  │     ├─ bootstrap.js
│           │  │     ├─ bootstrap.js.map
│           │  │     ├─ bootstrap.min.js
│           │  │     └─ bootstrap.min.js.map
│           │  └─ LICENSE
│           ├─ jquery
│           │  ├─ dist
│           │  │  ├─ jquery.js
│           │  │  ├─ jquery.min.js
│           │  │  └─ jquery.min.map
│           │  └─ LICENSE.txt
│           ├─ jquery-validation
│           │  ├─ dist
│           │  │  ├─ additional-methods.js
│           │  │  ├─ additional-methods.min.js
│           │  │  ├─ jquery.validate.js
│           │  │  └─ jquery.validate.min.js
│           │  └─ LICENSE.md
│           └─ jquery-validation-unobtrusive
│              ├─ jquery.validate.unobtrusive.js
│              ├─ jquery.validate.unobtrusive.min.js
│              └─ LICENSE.txt
└─ tests
   └─ ChecadorWeb.Tests
      ├─ AssemblyInfo.cs
      ├─ ChecadorWeb.Tests.csproj
      ├─ Integracion
      │  ├─ ContextoRevertido.cs
      │  └─ ServiciosIntegracionTests.cs
      ├─ MonitorFijo.cs
      ├─ Unit
      │  ├─ HeartbeatClasificadorTests.cs
      │  ├─ HorarioValidatorTests.cs
      │  ├─ LegacyPasswordHasherTests.cs
      │  └─ PermisosYValidacionTests.cs
      └─ Web
         ├─ ChecadorWebFactory.cs
         ├─ MarcajesExcelTests.cs
         └─ PermisosTests.cs

```

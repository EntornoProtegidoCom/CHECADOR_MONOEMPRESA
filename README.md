# Checador monoempresa

El proyecto contiene la API, el cliente de escritorio y el sitio web del
checador. Consulte los archivos README de cada componente para más detalles.

## Preparación antes de publicar

El archivo `.gitignore` excluye compilaciones, datos locales y archivos que
requieren revisión:

- Los archivos `appsettings.json` de la API y del cliente contienen
  configuración local y credenciales. Se conservan en este equipo, pero no
  se incluirán en Git. Para ejecutar esos componentes en otro equipo será
  necesario crear y configurar dichos archivos de forma segura.
- La documentación detallada y el archivo de pruebas HTTP de la API están
  excluidos preventivamente porque contienen o pueden contener credenciales.
  Revísenlos y eliminen los valores sensibles antes de compartirlos.
- El archivo comprimido de la base de datos está excluido hasta confirmar su
  contenido y que sea apropiado compartirlo.

Antes de crear un commit o publicar el repositorio, revise todos los archivos
que se van a incluir. El `.gitignore` no garantiza que no existan otros datos
sensibles. Si alguna credencial ha quedado expuesta, rótela.

```
CHECADOR_ENT_ONE
├─ APICHECADO
│  ├─ ChecadorAPI
│  │  ├─ appsettings.Development.json
│  │  ├─ ChecadorAPI.csproj
│  │  ├─ ChecadorAPI.http
│  │  ├─ Controllers
│  │  │  ├─ AsistenciaController.cs
│  │  │  ├─ AuthController.cs
│  │  │  ├─ BindingController.cs
│  │  │  ├─ HealthController.cs
│  │  │  └─ WorkersController.cs
│  │  ├─ Data
│  │  │  ├─ ISqlConnectionFactory.cs
│  │  │  ├─ SqlConnectionFactory.cs
│  │  │  └─ XmlPayloadBuilder.cs
│  │  ├─ docs
│  │  │  ├─ Diagnostico-Lote-Offline-2026-11-04.md
│  │  │  ├─ Diagnostico-Lote-Offline-SoloLectura.sql
│  │  │  └─ Fase6-Matriz-Pruebas.md
│  │  ├─ Middleware
│  │  │  └─ ApiKeyMiddleware.cs
│  │  ├─ Models
│  │  │  └─ DTOs
│  │  │     ├─ ApiResponseDto.cs
│  │  │     ├─ AsistenciaDtos.cs
│  │  │     ├─ AuthDtos.cs
│  │  │     ├─ BindingDtos.cs
│  │  │     ├─ HeartbeatDtos.cs
│  │  │     └─ WorkerScheduleResponseDto.cs
│  │  ├─ Program.cs
│  │  ├─ Properties
│  │  │  └─ launchSettings.json
│  │  └─ Services
│  │     ├─ AsistenciaService.cs
│  │     ├─ AuthService.cs
│  │     ├─ BindingService.cs
│  │     ├─ IAsistenciaService.cs
│  │     ├─ IAuthService.cs
│  │     ├─ IBindingService.cs
│  │     ├─ IWorkerScheduleService.cs
│  │     └─ WorkerScheduleService.cs
│  ├─ ChecadorAPI.Tests
│  │  ├─ ChecadorAPI.Tests.csproj
│  │  ├─ OfflineBatchContractTests.cs
│  │  └─ WorkerScheduleTests.cs
│  └─ README.md
├─ CLIENTCHECADO
│  ├─ ChecadorDesktop
│  │  ├─ ChecadorDesktop.csproj
│  │  ├─ Data
│  │  │  └─ SQLiteRepository.cs
│  │  ├─ Form1.cs
│  │  ├─ Form1.Designer.cs
│  │  ├─ Forms
│  │  │  ├─ AdminLoginForm.cs
│  │  │  ├─ AlertNotificationForm.cs
│  │  │  ├─ BindingForm.cs
│  │  │  ├─ InitialPinSetupForm.cs
│  │  │  ├─ MainForm.cs
│  │  │  ├─ MainForm.resx
│  │  │  └─ OverlayBackgroundForm.cs
│  │  ├─ Image
│  │  │  └─ checador-icon-3.ico
│  │  ├─ Models
│  │  │  └─ AppModels.cs
│  │  ├─ Program.cs
│  │  ├─ propuesta_tecnica_horario_refresh.md
│  │  ├─ QA_Matriz_Pruebas_Template.csv
│  │  ├─ README.MD
│  │  └─ Services
│  │     ├─ AlertScheduler.cs
│  │     ├─ ApiClientService.cs
│  │     ├─ CameraProviderContracts.cs
│  │     ├─ FlashCapCaptureService.cs
│  │     ├─ IPhotoCaptureService.cs
│  │     ├─ OpenCvCaptureService.cs
│  │     ├─ PhotoCaptureModels.cs
│  │     ├─ SyncManager.cs
│  │     └─ WebcamService.cs
│  ├─ ChecadorGuard
│  │  ├─ ChecadorGuard.csproj
│  │  └─ Program.cs
│  ├─ installer
│  │  └─ setup.iss
│  ├─ README.md
│  └─ scripts
│     ├─ Disable-TaskManager.ps1
│     └─ Register-Task.ps1
├─ DB_CHECADOR_ENT
├─ README.md
└─ WWWCHECADOR
   ├─ ChecadorWeb.slnx
   ├─ docs
   │  └─ sql
   │     ├─ DIA_FESTIVO.sql
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
   │     │  │  ├─ DiaFestivo.cs
   │     │  │  ├─ MM_HORARIO.cs
   │     │  │  ├─ MM_TRABAJADORE.cs
   │     │  │  └─ SS_USUARIO.cs
   │     │  └─ TransaccionExtensions.cs
   │     ├─ Infrastructure
   │     │  ├─ CabecerasSeguridad.cs
   │     │  ├─ FormatoDuracion.cs
   │     │  └─ PaginaBase.cs
   │     ├─ Models
   │     │  ├─ Comunes.cs
   │     │  ├─ DiasFestivos.cs
   │     │  ├─ Empleados.cs
   │     │  ├─ PaginadorVista.cs
   │     │  ├─ ReporteHoras.cs
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
   │     │  ├─ DiasFestivos
   │     │  │  ├─ Index.cshtml
   │     │  │  └─ Index.cshtml.cs
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
   │     │  ├─ ResumenH
   │     │  │  ├─ ResumenHoras.cshtml
   │     │  │  └─ ResumenHoras.cshtml.cs
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
   │     │  ├─ DiaFestivoService.cs
   │     │  ├─ EmpleadoService.cs
   │     │  ├─ EstacionYMarcajeServices.cs
   │     │  ├─ HeartbeatClasificador.cs
   │     │  ├─ HorarioValidator.cs
   │     │  ├─ PanelService.cs
   │     │  ├─ ResumenHorasExcel.cs
   │     │  ├─ ResumenHorasService.cs
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
         │  ├─ ResumenHorasIntegracionTests.cs
         │  └─ ServiciosIntegracionTests.cs
         ├─ MonitorFijo.cs
         ├─ Unit
         │  ├─ HeartbeatClasificadorTests.cs
         │  ├─ HorarioValidatorTests.cs
         │  ├─ LegacyPasswordHasherTests.cs
         │  ├─ PermisosYValidacionTests.cs
         │  └─ ResumenHorasTests.cs
         └─ Web
            ├─ ChecadorWebFactory.cs
            ├─ MarcajesExcelTests.cs
            ├─ PermisosTests.cs
            └─ ResumenHorasWebTests.cs

```
```
CHECADOR_ENT_ONE
├─ APICHECADO
│  ├─ ChecadorAPI
│  │  ├─ appsettings.Development.json
│  │  ├─ ChecadorAPI.csproj
│  │  ├─ ChecadorAPI.http
│  │  ├─ Controllers
│  │  │  ├─ AsistenciaController.cs
│  │  │  ├─ AuthController.cs
│  │  │  ├─ BindingController.cs
│  │  │  ├─ HealthController.cs
│  │  │  └─ WorkersController.cs
│  │  ├─ Data
│  │  │  ├─ ISqlConnectionFactory.cs
│  │  │  ├─ SqlConnectionFactory.cs
│  │  │  └─ XmlPayloadBuilder.cs
│  │  ├─ docs
│  │  │  ├─ Diagnostico-Lote-Offline-2026-11-04.md
│  │  │  ├─ Diagnostico-Lote-Offline-SoloLectura.sql
│  │  │  └─ Fase6-Matriz-Pruebas.md
│  │  ├─ Middleware
│  │  │  └─ ApiKeyMiddleware.cs
│  │  ├─ Models
│  │  │  └─ DTOs
│  │  │     ├─ ApiResponseDto.cs
│  │  │     ├─ AsistenciaDtos.cs
│  │  │     ├─ AuthDtos.cs
│  │  │     ├─ BindingDtos.cs
│  │  │     ├─ HeartbeatDtos.cs
│  │  │     └─ WorkerScheduleResponseDto.cs
│  │  ├─ Program.cs
│  │  ├─ Properties
│  │  │  └─ launchSettings.json
│  │  └─ Services
│  │     ├─ AsistenciaService.cs
│  │     ├─ AuthService.cs
│  │     ├─ BindingService.cs
│  │     ├─ IAsistenciaService.cs
│  │     ├─ IAuthService.cs
│  │     ├─ IBindingService.cs
│  │     ├─ IWorkerScheduleService.cs
│  │     └─ WorkerScheduleService.cs
│  ├─ ChecadorAPI.Tests
│  │  ├─ ChecadorAPI.Tests.csproj
│  │  ├─ OfflineBatchContractTests.cs
│  │  └─ WorkerScheduleTests.cs
│  └─ README.md
├─ CLIENTCHECADO
│  ├─ ChecadorDesktop
│  │  ├─ ChecadorDesktop.csproj
│  │  ├─ Data
│  │  │  └─ SQLiteRepository.cs
│  │  ├─ Form1.cs
│  │  ├─ Form1.Designer.cs
│  │  ├─ Forms
│  │  │  ├─ AdminLoginForm.cs
│  │  │  ├─ AlertNotificationForm.cs
│  │  │  ├─ BindingForm.cs
│  │  │  ├─ InitialPinSetupForm.cs
│  │  │  ├─ MainForm.cs
│  │  │  ├─ MainForm.resx
│  │  │  └─ OverlayBackgroundForm.cs
│  │  ├─ Image
│  │  │  └─ checador-icon-3.ico
│  │  ├─ Models
│  │  │  └─ AppModels.cs
│  │  ├─ Program.cs
│  │  ├─ propuesta_tecnica_horario_refresh.md
│  │  ├─ QA_Matriz_Pruebas_Template.csv
│  │  ├─ README.MD
│  │  └─ Services
│  │     ├─ AlertScheduler.cs
│  │     ├─ ApiClientService.cs
│  │     ├─ CameraProviderContracts.cs
│  │     ├─ FlashCapCaptureService.cs
│  │     ├─ IPhotoCaptureService.cs
│  │     ├─ OpenCvCaptureService.cs
│  │     ├─ PhotoCaptureModels.cs
│  │     ├─ SyncManager.cs
│  │     └─ WebcamService.cs
│  ├─ ChecadorGuard
│  │  ├─ ChecadorGuard.csproj
│  │  └─ Program.cs
│  ├─ installer
│  │  └─ setup.iss
│  ├─ README.md
│  └─ scripts
│     ├─ Disable-TaskManager.ps1
│     └─ Register-Task.ps1
├─ DB_CHECADOR_ENT
├─ README.md
└─ WWWCHECADOR
   ├─ ChecadorWeb.slnx
   ├─ docs
   │  └─ sql
   │     ├─ DIA_FESTIVO.sql
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
   │     │  │  ├─ DiaFestivo.cs
   │     │  │  ├─ MM_HORARIO.cs
   │     │  │  ├─ MM_TRABAJADORE.cs
   │     │  │  └─ SS_USUARIO.cs
   │     │  └─ TransaccionExtensions.cs
   │     ├─ Infrastructure
   │     │  ├─ CabecerasSeguridad.cs
   │     │  ├─ FormatoDuracion.cs
   │     │  └─ PaginaBase.cs
   │     ├─ Models
   │     │  ├─ Comunes.cs
   │     │  ├─ DiasFestivos.cs
   │     │  ├─ Empleados.cs
   │     │  ├─ PaginadorVista.cs
   │     │  ├─ ReporteHoras.cs
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
   │     │  ├─ DiasFestivos
   │     │  │  ├─ Index.cshtml
   │     │  │  └─ Index.cshtml.cs
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
   │     │  ├─ ResumenH
   │     │  │  ├─ ResumenHoras.cshtml
   │     │  │  └─ ResumenHoras.cshtml.cs
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
   │     │  ├─ DiaFestivoService.cs
   │     │  ├─ EmpleadoService.cs
   │     │  ├─ EstacionYMarcajeServices.cs
   │     │  ├─ HeartbeatClasificador.cs
   │     │  ├─ HorarioValidator.cs
   │     │  ├─ PanelService.cs
   │     │  ├─ ResumenHorasExcel.cs
   │     │  ├─ ResumenHorasService.cs
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
         │  ├─ ResumenHorasIntegracionTests.cs
         │  └─ ServiciosIntegracionTests.cs
         ├─ MonitorFijo.cs
         ├─ Unit
         │  ├─ HeartbeatClasificadorTests.cs
         │  ├─ HorarioValidatorTests.cs
         │  ├─ LegacyPasswordHasherTests.cs
         │  ├─ PermisosYValidacionTests.cs
         │  └─ ResumenHorasTests.cs
         └─ Web
            ├─ ChecadorWebFactory.cs
            ├─ MarcajesExcelTests.cs
            ├─ PermisosTests.cs
            └─ ResumenHorasWebTests.cs

```
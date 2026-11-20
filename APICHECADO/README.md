
```
APICHECADO
├─ ChecadorAPI
│  ├─ appsettings.Development.json
│  ├─ appsettings.json
│  ├─ ChecadorAPI.csproj
│  ├─ ChecadorAPI.http
│  ├─ Controllers
│  │  ├─ AsistenciaController.cs
│  │  ├─ AuthController.cs
│  │  ├─ BindingController.cs
│  │  ├─ HealthController.cs
│  │  └─ WorkersController.cs
│  ├─ Data
│  │  ├─ ISqlConnectionFactory.cs
│  │  ├─ SqlConnectionFactory.cs
│  │  └─ XmlPayloadBuilder.cs
│  ├─ docs
│  │  ├─ Diagnostico-Lote-Offline-2026-11-04.md
│  │  ├─ Diagnostico-Lote-Offline-SoloLectura.sql
│  │  └─ Fase6-Matriz-Pruebas.md
│  ├─ Middleware
│  │  └─ ApiKeyMiddleware.cs
│  ├─ Models
│  │  └─ DTOs
│  │     ├─ ApiResponseDto.cs
│  │     ├─ AsistenciaDtos.cs
│  │     ├─ AuthDtos.cs
│  │     ├─ BindingDtos.cs
│  │     ├─ HeartbeatDtos.cs
│  │     └─ WorkerScheduleResponseDto.cs
│  ├─ Program.cs
│  ├─ Properties
│  │  └─ launchSettings.json
│  ├─ README.MD
│  ├─ Services
│  │  ├─ AsistenciaService.cs
│  │  ├─ AuthService.cs
│  │  ├─ BindingService.cs
│  │  ├─ IAsistenciaService.cs
│  │  ├─ IAuthService.cs
│  │  ├─ IBindingService.cs
│  │  ├─ IWorkerScheduleService.cs
│  │  └─ WorkerScheduleService.cs
│  └─ WorkerSchedule.http
└─ ChecadorAPI.Tests
   ├─ ChecadorAPI.Tests.csproj
   ├─ OfflineBatchContractTests.cs
   └─ WorkerScheduleTests.cs

```
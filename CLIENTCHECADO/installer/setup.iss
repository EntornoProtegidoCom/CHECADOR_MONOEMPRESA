; installer/setup.iss
#define MyAppName "ChecadorDesktop"
#define MyAppVersion "1.0.5"
#define MyAppPublisher "Entorno Protegido"
#define MyAppExeName "ChecadorDesktop.exe"
#define RequiredDotNetRuntime "8.0.33"
#define DotNetDownloadUrl "https://dotnet.microsoft.com/es-es/download/dotnet/thank-you/runtime-desktop-8.0.33-windows-x64-installer?cid=getdotnetcore"

[Setup]
AppId={{D3F4A5B6-C7D8-4E9F-0A1B-2C3D4E5F6A7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\Servicios\ChecadorDesktop
DisableProgramGroupPage=yes
OutputBaseFilename=Setup_ChecadorDesktop
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\ChecadorDesktop\Image\checador-icon-3.ico

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
; Copiar todos los archivos de la carpeta de publicacion dotnet
Source: "..\ChecadorDesktop\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ChecadorGuard\bin\Release\net8.0-windows\GuardEP.*"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\Register-Task.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\Disable-TaskManager.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\Checador Desktop"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\Checador Desktop"; Filename: "{app}\{#MyAppExeName}"



[Run]
; Ejecutar script de PowerShell para registrar la Tarea Programada
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\scripts\Register-Task.ps1"" -InstallDir ""{app}"" -InstallPath ""{app}\{#MyAppExeName}"" -LogPath ""{app}\scripts\Register-Task.log"""; Flags: runhidden waituntilterminated
; Ejecutar script para deshabilitar el Administrador de Tareas
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\scripts\Disable-TaskManager.ps1"" -Disable $true -InstallDir ""{app}"""; Flags: runhidden

[UninstallRun]
; Limpiar la tarea programada al desinstalar
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""ChecadorDesktopWatchdogTask"" /F"; Flags: runhidden; RunOnceId: "DeleteChecadorDesktopWatchdogTask"

[Code]
function IsRequiredDotNetInstalled: Boolean;
begin
	Result :=
		DirExists(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\{#RequiredDotNetRuntime}')) or
		DirExists(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App\{#RequiredDotNetRuntime}'));
end;

function InitializeSetup: Boolean;
var
	Response: Integer;
	ResultCode: Integer;
begin
	if IsRequiredDotNetInstalled then
	begin
		Result := True;
		exit;
	end;

	Response := MsgBox(
		'Este instalador requiere Microsoft .NET Desktop Runtime x64 {#RequiredDotNetRuntime}.' + #13#10 + #13#10 +
		'Instalelo y vuelva a ejecutar Setup_ChecadorDesktop.' + #13#10 + #13#10 +
		'Desea abrir la pagina de descarga ahora?',
		mbError, MB_YESNO);

	if Response = IDYES then
		ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);

	Result := False;
end;

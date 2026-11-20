param(
    [string]$InstallDir = "$env:ProgramFiles\ChecadorDesktop",
    [string]$InstallPath = "",
    [string]$LogPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$TaskName = "ChecadorDesktopWatchdogTask"
$SchtasksPath = ""

if ([string]::IsNullOrWhiteSpace($InstallPath)) {
    $InstallPath = Join-Path $InstallDir "ChecadorDesktop.exe"
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $InstallDir "scripts\Register-Task.log"
}

function Write-Log {
    param([string]$Message)

    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    try {
        Add-Content -Path $LogPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
    }
    catch {
        # No interrumpir por fallos de log.
    }
}

function Resolve-SchtasksPath {
    # En PowerShell de 32 bits sobre Windows x64, System32 se redirige a SysWOW64.
    # Priorizar Sysnative evita redireccion WOW64 al invocar binarios del sistema.
    if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess -and
        (Test-Path -LiteralPath "$env:WINDIR\Sysnative\schtasks.exe")) {
        return "$env:WINDIR\Sysnative\schtasks.exe"
    }

    if (Test-Path -LiteralPath "$env:WINDIR\System32\schtasks.exe") {
        return "$env:WINDIR\System32\schtasks.exe"
    }

    if (Test-Path -LiteralPath "$env:WINDIR\Sysnative\schtasks.exe") {
        return "$env:WINDIR\Sysnative\schtasks.exe"
    }

    throw "No se encontro schtasks.exe en rutas del sistema."
}

function Test-TaskExists {
    param([string]$Name)

    # /Query devuelve error en stderr cuando la tarea no existe; eso es esperado.
    # Con ErrorActionPreference=Stop debemos aislar este caso para que no aborte el flujo.
    $previousEap = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        # Probar con y sin prefijo de ruta de carpeta raiz.
        & $SchtasksPath /Query /TN $Name 2>$null 1>$null
        if ($LASTEXITCODE -eq 0) { return $true }

        & $SchtasksPath /Query /TN ("\" + $Name) 2>$null 1>$null
        if ($LASTEXITCODE -eq 0) { return $true }

        # Fallback adicional por API de ScheduledTasks.
        Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue *> $null
        if ($?) { return $true }

        return $false
    }
    finally {
        $ErrorActionPreference = $previousEap
    }
}

function Wait-TaskRegistration {
    param(
        [string]$Name,
        [int]$Attempts = 10,
        [int]$DelayMs = 500
    )

    for ($i = 1; $i -le $Attempts; $i++) {
        if (Test-TaskExists -Name $Name) {
            return $true
        }

        Start-Sleep -Milliseconds $DelayMs
    }

    return $false
}

function Remove-TaskIfExists {
    param([string]$Name)

    if (Test-TaskExists -Name $Name) {
        & $SchtasksPath /Delete /TN $Name /F *> $null
        Write-Log "Tarea existente eliminada: $Name"
    }
}

function Invoke-TaskCreateWithSchtasks {
    param([string[]]$TaskArgs, [string]$Mode)

    Write-Log "Intentando crear tarea con modo: $Mode"
    $output = & $SchtasksPath @TaskArgs 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        Write-Log "Fallo modo $Mode. ExitCode=$code. Salida=$($output -join ' | ')"
        return $false
    }

    Write-Log "Salida modo ${Mode}: $($output -join ' | ')"

    if (-not (Wait-TaskRegistration -Name $TaskName -Attempts 12 -DelayMs 500)) {
        Write-Log "Modo $Mode reporto exito, pero la tarea no existe tras reintentos de consulta."
        return $false
    }

    Write-Log "Modo $Mode exitoso."
    return $true
}

try {
    Write-Log "Inicio de registro de tarea. InstallDir=$InstallDir InstallPath=$InstallPath"

    $SchtasksPath = Resolve-SchtasksPath
    Write-Log "PowerShell=$($PSHOME) SchtasksPath=$SchtasksPath"

    if (-not (Test-Path -LiteralPath $InstallPath)) {
        throw "No se encontro el ejecutable para registrar la tarea: $InstallPath"
    }

    $taskRun = '"' + $InstallPath + '"'
    Remove-TaskIfExists -Name $TaskName

    # Modo 1: usuario interactivo actual
    $argsMode1 = @(
        "/Create",
        "/TN", $TaskName,
        "/SC", "ONLOGON",
        "/RL", "HIGHEST",
        "/TR", $taskRun,
        "/F"
    )

    $created = Invoke-TaskCreateWithSchtasks -TaskArgs $argsMode1 -Mode "CurrentUserOnLogon"

    # Modo 2: principal INTERACTIVE para escenarios multiusuario.
    if (-not $created) {
        $argsMode2 = @(
            "/Create",
            "/TN", $TaskName,
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/RU", "INTERACTIVE",
            "/IT",
            "/TR", $taskRun,
            "/F"
        )
        $created = Invoke-TaskCreateWithSchtasks -TaskArgs $argsMode2 -Mode "InteractiveAnyUser"
    }

    if (-not $created) {
        throw "No fue posible crear la tarea programada '$TaskName' en ningun modo. Revise: $LogPath"
    }

    Write-Log "Tarea registrada exitosamente: $TaskName"
    Write-Host "Tarea programada '$TaskName' registrada exitosamente."
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    if ($_.InvocationInfo) {
        Write-Log "ERROR_LINE: $($_.InvocationInfo.PositionMessage -replace '\r|\n',' ')"
    }
    Write-Error "Error al registrar la tarea programada '$TaskName': $($_.Exception.Message)"
    exit 1
}

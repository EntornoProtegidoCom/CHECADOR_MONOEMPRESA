param(
    [bool]$Disable = $true,
    [string]$InstallDir = ""
)

$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"

if (!(Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}

if ($Disable) {
    Set-ItemProperty -Path $registryPath -Name "DisableTaskMgr" -Value 1 -Type DWord
    Write-Host "Administrador de Tareas deshabilitado correctamente."
}
else {
    Remove-ItemProperty -Path $registryPath -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
    Write-Host "Administrador de Tareas habilitado nuevamente."
}

# Builds a single-file standalone JavaSwitcher.exe (C# WinForms, .NET Framework 4.x)
# Usage: powershell -ExecutionPolicy Bypass -File .\build.ps1

$ErrorActionPreference = 'Stop'

$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$src   = Join-Path $root 'src'
$exe   = Join-Path $root 'JavaSwitcher.exe'
$manifest = Join-Path $src 'app.manifest'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "csc.exe not found: $csc"
}

$files = Get-ChildItem -Path $src -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if (-not $files -or $files.Count -eq 0) {
    throw 'No C# source files found under src\'
}

$buildArgs = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/codepage:65001',
    "/win32manifest:$manifest",
    "/out:$exe",
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll'
)
$buildArgs += $files

& $csc @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $exe"

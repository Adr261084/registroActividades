param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "RegActividades.App\RegActividades.App.csproj"
$publishDir = Join-Path $repoRoot "RegActividades.App\bin\$Configuration\net8.0-windows\$Runtime\publish"
$artifactsDir = Join-Path $repoRoot "artifacts"
$portableDir = Join-Path $artifactsDir "portable"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "RegActividades-portable-$Runtime-$timestamp.zip"
$zipPath = Join-Path $portableDir $zipName

if (!(Test-Path $projectPath)) {
    throw "No se encontro el proyecto en: $projectPath"
}

Write-Host "Publicando aplicacion..." -ForegroundColor Cyan

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", ($SelfContained.IsPresent ? "true" : "false"),
    "/p:PublishSingleFile=$($SingleFile.IsPresent ? "true" : "false")"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo $LASTEXITCODE"
}

if (!(Test-Path $publishDir)) {
    throw "No se encontro la carpeta publish en: $publishDir"
}

New-Item -ItemType Directory -Path $portableDir -Force | Out-Null

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creando ZIP portable..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Listo. Paquete generado:" -ForegroundColor Green
Write-Host $zipPath -ForegroundColor Green

# Genera el publish autocontenido (self-contained, single-file) de SisLabTopo.UI
# que consume installer\setup.iss (ver [Files] -> PublishDir = ..\publish\win-x64).
#
# Uso (desde la raíz del repo o desde installer\):
#   powershell -ExecutionPolicy Bypass -File installer\publish.ps1
#
# El resultado es un único SisLabTopo.UI.exe con el runtime .NET 10 embebido:
# corre en una máquina sin el SDK/runtime de .NET instalado. No requiere
# empaquetar una JRE/JDK aparte (a diferencia de la versión Java + launch4j).

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot "publish\win-x64"

Write-Host "Publicando SisLabTopo.UI (self-contained, single-file, win-x64) en $outDir ..."

dotnet publish (Join-Path $repoRoot "src\SisLabTopo.UI") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló con código $LASTEXITCODE"
}

Write-Host "Publish listo: $outDir\SisLabTopo.UI.exe"
Write-Host "Ahora puedes compilar el instalador con: ISCC.exe installer\setup.iss"
Write-Host "(Opcional) Firmar el .exe antes de empaquetar: ver installer\sign.ps1"

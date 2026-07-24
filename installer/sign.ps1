# Firma digitalmente (Authenticode) el .exe publicado y/o el instalador compilado.
#
# Uso:
#   # Demo/dev: genera un certificado autofirmado de un solo uso y firma con el.
#   powershell -ExecutionPolicy Bypass -File installer\sign.ps1 -FilePath publish\win-x64\SisLabTopo.UI.exe -CreateSelfSignedDevCert
#
#   # Real: firma con un certificado Authenticode comprado (ver nota mas abajo).
#   powershell -ExecutionPolicy Bypass -File installer\sign.ps1 -FilePath dist\SisLab-Topo-Setup-1.0.0.exe -CertPath C:\ruta\certificado.pfx -CertPassword (Read-Host -AsSecureString)
#
# ---------------------------------------------------------------------------
# LO QUE ESTO SI HACE: agrega una firma Authenticode verificable (el .exe deja
# de decir "Editor: Desconocido" en el dialogo de "¿Deseas ejecutar este
# archivo?" y muestra el nombre del firmante), y con -CreateSelfSignedDevCert
# deja el pipeline de firma listo/probado sin gastar nada.
#
# LO QUE ESTO NO HACE: un certificado AUTOFIRMADO (-CreateSelfSignedDevCert)
# NO quita la advertencia de SmartScreen de Windows ("Windows protegio su
# equipo") -- SmartScreen exige una cadena de confianza hasta una entidad
# certificadora (CA) publica reconocida por Microsoft, y ademas evalua
# reputacion por volumen de descargas/tiempo (un binario nuevo, aunque este
# firmado con un certificado real recien comprado, puede seguir mostrando la
# advertencia unos dias/semanas hasta acumular reputacion -- esto ni pagando
# se evita instantaneamente).
#
# Para eliminar la advertencia de verdad hay dos caminos reales, ninguno
# gratuito ni instantaneo (por eso no se resuelve solo con codigo):
#   1. Certificado Authenticode OV/EV de una CA (DigiCert, Sectigo, SSL.com...):
#      ~100-400 USD/año, requiere verificar la identidad de la institución
#      (UNAP) ante la CA. Un certificado EV en un dispositivo con reputacion
#      previa puede saltarse SmartScreen casi de inmediato; uno OV normal
#      igual acumula reputacion con el tiempo.
#   2. Azure Trusted Signing (Microsoft): ~10 USD/mes, mas rapido de tramitar
#      que un cert tradicional, pero igual exige verificar una identidad de
#      organizacion real ante Microsoft.
# Ambos requieren una decision/gasto institucional (UNAP), no algo que se
# pueda ejecutar desde este script.
# ---------------------------------------------------------------------------

param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [switch]$CreateSelfSignedDevCert,

    [string]$CertPath,

    [System.Security.SecureString]$CertPassword
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $FilePath)) {
    throw "No existe el archivo a firmar: $FilePath"
}

function Find-SignTool {
    $candidato = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*x64*" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $candidato) {
        throw "No se encontro signtool.exe (viene con el Windows SDK). Instalalo o agregalo al PATH."
    }
    return $candidato.FullName
}

$signtool = Find-SignTool

if ($CreateSelfSignedDevCert) {
    Write-Host "Generando certificado autofirmado temporal (SOLO para probar el pipeline de firma -- no quita la advertencia de SmartScreen, ver comentario al inicio de este script)..."
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=SisLab-Topo (certificado de desarrollo, NO usar en produccion)" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(1)

    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /tr http://timestamp.digicert.com /td SHA256 "$FilePath"
}
elseif ($CertPath) {
    if (-not (Test-Path $CertPath)) {
        throw "No existe el certificado: $CertPath"
    }
    if (-not $CertPassword) {
        $CertPassword = Read-Host -AsSecureString -Prompt "Contraseña del certificado ($CertPath)"
    }
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertPassword))

    & $signtool sign /fd SHA256 /f "$CertPath" /p "$plainPassword" /tr http://timestamp.digicert.com /td SHA256 "$FilePath"
}
else {
    throw "Especifica -CreateSelfSignedDevCert (demo local) o -CertPath (certificado Authenticode real)."
}

if ($LASTEXITCODE -ne 0) {
    throw "signtool fallo con codigo $LASTEXITCODE"
}

Write-Host "Firmado: $FilePath"

if ($CreateSelfSignedDevCert) {
    # "verify /pa" fallaria aqui con "certificate chain... terminated in a root
    # certificate which is not trusted" -- es el comportamiento ESPERADO de un
    # certificado autofirmado (no hay CA publica detras) y no indica que la
    # firma en si haya fallado (ver "Successfully signed" arriba). Se omite la
    # verificacion estricta en este camino para no imprimir un error nativo
    # confuso donde no hay ningun problema real que corregir.
    Write-Host "(Certificado autofirmado: se omite 'signtool verify /pa' -- fallaria por diseno, no hay CA publica detras. La firma en si ya se confirmo arriba con 'Successfully signed'.)"
}
else {
    & $signtool verify /pa "$FilePath"
    if ($LASTEXITCODE -ne 0) {
        throw "La firma se aplico pero 'signtool verify /pa' fallo (codigo $LASTEXITCODE) -- revisa que el certificado sea de un emisor confiable."
    }
}

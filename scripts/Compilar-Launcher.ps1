$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$codigo = Join-Path $raiz 'launcher'
$destino = Join-Path $raiz 'dist\launcher'
$compilador = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compilador)) {
    throw 'O compilador do .NET Framework nao foi encontrado.'
}

New-Item -ItemType Directory -Path $destino -Force | Out-Null
$executavel = Join-Path $destino 'milton-fretes-launcher.exe'
$manifesto = Join-Path $codigo 'launcher.manifest'
$icone = Join-Path $codigo 'milton-fretes.ico'
$fontes = Get-ChildItem -LiteralPath $codigo -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
$referencias = @('System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Net.Http.dll',
    'System.Web.Extensions.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
$parametros = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/debug-', '/codepage:65001',
    "/out:$executavel", "/win32manifest:$manifesto", "/win32icon:$icone", "/reference:$($referencias -join ',')")
& $compilador @parametros @fontes
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o launcher. Codigo $LASTEXITCODE."
}
Get-Item -LiteralPath $executavel

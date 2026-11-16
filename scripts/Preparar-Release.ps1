[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Pacote,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Versao
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$origem = Get-Item -LiteralPath $Pacote
if ($origem.PSIsContainer -or $origem.Extension -ne '.scs') {
    throw 'Informe um arquivo .scs.'
}

$arquivo = [System.IO.Compression.ZipFile]::OpenRead($origem.FullName)
try {
    $manifesto = $arquivo.GetEntry('manifest.sii')
    if ($null -eq $manifesto) {
        throw 'manifest.sii ausente na raiz do pacote.'
    }

    $leitor = [System.IO.StreamReader]::new($manifesto.Open())
    try {
        $texto = $leitor.ReadToEnd()
    } finally {
        $leitor.Dispose()
    }

    $versaoNoPacote = [regex]::Match($texto, '(?m)^\s*package_version\s*:\s*"([^"]+)"')
    if (!$versaoNoPacote.Success -or $versaoNoPacote.Groups[1].Value -cne $Versao) {
        throw "A versao no manifest.sii deve ser $Versao."
    }
} finally {
    $arquivo.Dispose()
}

$raiz = Split-Path -Parent $PSScriptRoot
$pasta = Join-Path $raiz "dist/v$Versao"
$nomeDoMod = 'milton-fretes.scs'
$destino = Join-Path $pasta $nomeDoMod
New-Item -ItemType Directory -Path $pasta -Force | Out-Null

if ($origem.FullName -ne $destino) {
    Copy-Item -LiteralPath $origem.FullName -Destination $destino -Force
}

$hash = (Get-FileHash -LiteralPath $destino -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $pasta 'SHA256SUMS.txt') -Value "$hash  $nomeDoMod" -Encoding ASCII
Get-Item -LiteralPath $destino, (Join-Path $pasta 'SHA256SUMS.txt')

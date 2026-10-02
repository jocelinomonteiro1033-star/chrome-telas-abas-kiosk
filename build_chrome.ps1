$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Compilador C# do Windows nao encontrado: $csc" }

$output = Join-Path $PSScriptRoot 'ChromeTelasAbasKiosk.exe'
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /define:CHROME_ONLY /out:$output `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Management.dll `
    /reference:System.Web.Extensions.dll `
    (Join-Path $PSScriptRoot 'DualScreenKiosk.cs')

if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao (codigo $LASTEXITCODE)." }
Write-Host "Executavel criado: $output"

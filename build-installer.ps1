$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $projectRoot 'build.ps1')

$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php and run this script again.'
}

& $compiler (Join-Path $projectRoot 'installer\MK212DriveGlow.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Write-Host "Built $(Join-Path $projectRoot 'dist\MK212-DriveGlow-Setup-v1.2.2.exe')"

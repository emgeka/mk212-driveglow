$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$distribution = Join-Path $projectRoot 'dist'
$icon = Join-Path $projectRoot 'assets\icon.ico'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework C# compiler was not found.'
}

New-Item -ItemType Directory -Force -Path $distribution | Out-Null

if (-not (Test-Path -LiteralPath $icon)) {
    $iconMaker = Join-Path $env:TEMP 'Mk212MakeIcon.exe'
    & $compiler /nologo /target:exe /reference:System.Drawing.dll "/out:$iconMaker" (Join-Path $projectRoot 'tools\MakeIcon.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Icon build failed.' }
    & $iconMaker $icon
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
}

$output = Join-Path $distribution 'MK212-DriveGlow.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/win32icon:$icon" "/out:$output" `
    (Join-Path $projectRoot 'src\AssemblyInfo.cs') `
    (Join-Path $projectRoot 'src\DiskActivityLight.cs')

if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Host "Built $output"

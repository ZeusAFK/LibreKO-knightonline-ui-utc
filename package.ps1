param([string]$Out = "dist")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

dotnet build (Join-Path $root "KnightOnlineUi.csproj") -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$manifest = Get-Content (Join-Path $root "plugin.json") -Raw | ConvertFrom-Json
$name = "$($manifest.id)-$($manifest.version)"
$stage = Join-Path ([IO.Path]::GetTempPath()) "$name-stage"
$folder = Join-Path $stage $manifest.id
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory (Join-Path $folder "bin") -Force | Out-Null

foreach ($item in "plugin.json", "README.md", "LICENSE", "assets") {
    Copy-Item (Join-Path $root $item) $folder -Recurse
}
foreach ($item in "KnightOnlineUi.dll", "KnightOnlineUi.pdb") {
    Copy-Item (Join-Path $root "bin" $item) (Join-Path $folder "bin")
}

New-Item -ItemType Directory (Join-Path $root $Out) -Force | Out-Null
$zip = Join-Path $root $Out "$name.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $folder -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force
Write-Host "wrote $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

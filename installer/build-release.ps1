# Builds the release artifacts into release/:
#   MakroPad-Setup-v<version>.exe     Windows installer (self-contained, no .NET install needed)
#   MakroPad-firmware-v<version>.bin  merged ESP32-S3 image, flash at offset 0x0
# Version comes from app/Directory.Build.props.
param(
    [switch]$SkipFirmware
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root 'release'
$publish = Join-Path $release 'publish'

[xml]$props = Get-Content (Join-Path $root 'app\Directory.Build.props')
$version = @($props.Project.PropertyGroup.Version | Where-Object { $_ })[0]
Write-Host "MakroPad $version"

if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
New-Item -ItemType Directory -Force $release | Out-Null

# App
dotnet test (Join-Path $root 'app\MakroPad.sln') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet publish (Join-Path $root 'app\MakroPad.App\MakroPad.App.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -o $publish --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

# Installer
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found (winget install JRSoftware.InnoSetup)' }
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publish" "/DOutputDir=$release" (Join-Path $PSScriptRoot 'MakroPad.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

# Firmware
if (-not $SkipFirmware) {
    $fw = Join-Path $root 'firmware'
    python -m platformio run -d $fw -e esp32-s3
    if ($LASTEXITCODE -ne 0) { throw 'Firmware build failed' }
    python -m platformio test -d $fw -e native
    if ($LASTEXITCODE -ne 0) { throw 'Firmware tests failed' }

    $pio = Join-Path $env:USERPROFILE '.platformio\packages'
    $build = Join-Path $fw '.pio\build\esp32-s3'
    python (Join-Path $pio 'tool-esptoolpy\esptool.py') --chip esp32s3 merge_bin `
        -o (Join-Path $release "MakroPad-firmware-v$version.bin") `
        --flash_mode keep --flash_freq keep --flash_size keep `
        0x0 (Join-Path $build 'bootloader.bin') `
        0x8000 (Join-Path $build 'partitions.bin') `
        0xe000 (Join-Path $pio 'framework-arduinoespressif32\tools\partitions\boot_app0.bin') `
        0x10000 (Join-Path $build 'firmware.bin')
    if ($LASTEXITCODE -ne 0) { throw 'Merging firmware failed' }
}

$artifacts = Get-ChildItem $release -File -Filter "MakroPad-*-v$version.*"
$artifacts | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    "{0}  {1}" -f $hash, $_.Name
} | Set-Content -Encoding ascii (Join-Path $release 'SHA256SUMS.txt')

$artifacts | Format-Table Name, Length

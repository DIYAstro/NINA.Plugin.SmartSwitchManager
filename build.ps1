# N.I.N.A. Smart Switch Manager - Build Script

param (
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0.0"
)

$ProjectName = "NINA.Plugin.SmartSwitchManager"
$ProjectPath = Join-Path $ProjectName "$ProjectName.csproj"
$BuildDir = Join-Path $PSScriptRoot "build"
$DistDir = Join-Path $BuildDir $ProjectName
$ZipFile = Join-Path $BuildDir "$ProjectName.zip"
$TempDir = Join-Path $BuildDir "temp"

Write-Host "--- Starting Build for $ProjectPath ---" -ForegroundColor Cyan

# Ensure build directory exists and is clean
if (Test-Path $BuildDir) {
    Write-Host "Cleaning build directory: $BuildDir" -ForegroundColor Gray
    Remove-Item -Path "$BuildDir\*" -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $BuildDir | Out-Null
}

# Create distribution and temp folders
New-Item -ItemType Directory -Path $DistDir | Out-Null
New-Item -ItemType Directory -Path $TempDir | Out-Null

# Run dotnet build on the solution
Write-Host "Running dotnet build ($Configuration) for v$Version on the solution..." -ForegroundColor Yellow
dotnet build "NINA.Plugin.SmartSwitchManager.sln" -c $Configuration -p:Version=$Version

if ($LASTEXITCODE -eq 0) {
    Write-Host "Isolating plugin files..." -ForegroundColor Gray
    
    # 1. Copy main plugin and dependencies to distribution folder
    $MainOutputDir = Join-Path $PSScriptRoot "$ProjectName\bin\x64\$Configuration\net8.0-windows7.0"
    if (-not (Test-Path $MainOutputDir)) {
        $MainOutputDir = Join-Path $PSScriptRoot "$ProjectName\bin\$Configuration\net8.0-windows7.0"
    }
    
    Copy-Item "$MainOutputDir\*.dll" -Destination $DistDir
    
    # 2. Copy providers to their subfolders within the distribution folder
    $BackendsBaseDir = New-Item -ItemType Directory -Path "$DistDir\Backends" -Force
    
    $Providers = @("Shelly", "Tasmota", "HomeAssistant", "ESPHome")
    foreach ($provider in $Providers) {
        $providerProject = "NINA.Plugin.SmartSwitchManager.$provider"
        $providerOutputDir = Join-Path $PSScriptRoot "Providers\$provider\bin\x64\$Configuration\net8.0-windows7.0"
        
        if (-not (Test-Path $providerOutputDir)) {
            $providerOutputDir = Join-Path $PSScriptRoot "Providers\$provider\bin\$Configuration\net8.0-windows7.0"
        }
        
        if (Test-Path $providerOutputDir) {
            $dest = New-Item -ItemType Directory -Path "$BackendsBaseDir\$provider" -Force
            Copy-Item "$providerOutputDir\$providerProject.dll" -Destination $dest
            Write-Host "  Deployed provider: $provider" -ForegroundColor Gray
        } else {
            Write-Warning "  Provider output not found: $provider"
        }
    }

    # 3. Create ZIP archive
    Write-Host "`nCreating ZIP archive: $ZipFile" -ForegroundColor Yellow
    Compress-Archive -Path "$DistDir\*" -DestinationPath $ZipFile -Force

    # Cleanup temporary build files
    Remove-Item -Path $TempDir -Recurse -Force

    Write-Host "`nBuild SUCCESSFUL!" -ForegroundColor Green
    Write-Host "The distribution folder is ready: $DistDir" -ForegroundColor White
    Write-Host "The ZIP archive is ready: $ZipFile" -ForegroundColor White
    Write-Host "Contains: $ProjectName.dll and its dependencies" -ForegroundColor Gray
} else {
    Write-Error "Build FAILED with exit code $LASTEXITCODE"
}

Write-Host "--- Build Finished ---" -ForegroundColor Cyan

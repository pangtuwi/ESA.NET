# Builds ESA.exe, a single-file Windows executable that needs nothing installed,
# and a zip of it ready to share.
#
#   packaging\windows\make-exe.ps1                     # for this PC's own chip
#   packaging\windows\make-exe.ps1 -Runtime win-arm64  # for an Arm PC
#   packaging\windows\make-exe.ps1 -Version 1.2
#
# Windows refuses to run scripts by default. If it says "running scripts is
# disabled on this system", start the script like this instead:
#
#   powershell -ExecutionPolicy Bypass -File packaging\windows\make-exe.ps1
#
# It leaves, under artifacts\windows\<runtime>\ (which git ignores):
#
#   ESA\ESA.exe                  the program
#   ESA\Help\User_Manual.pdf     opened by Help > User Manual, so it must stay beside it
#   ESA-<version>-<runtime>.zip   the ESA folder, zipped, to hand to someone
#
# ESA.exe carries the .NET runtime and Avalonia's native libraries inside it, so it
# runs on a PC with no .NET installed, from any folder. It is not code-signed, so a copy
# downloaded from the internet gets SmartScreen's "Windows protected your PC"
# warning until it is; More info > Run anyway lets it through.
#
# Nothing in it needs Windows: under PowerShell 7 (pwsh) it builds the same
# executable on macOS or Linux.

param(
    [ValidateSet("win-x64", "win-arm64")]
    [string] $Runtime,

    [string] $Version = "1.0"
)

$ErrorActionPreference = "Stop"

if (-not $Runtime) {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($IsWindows -ne $false -and $architecture -eq "Arm64") {
        $Runtime = "win-arm64"
    } else {
        $Runtime = "win-x64"
    }
}

# Work from the repository root, wherever the script is called from.
Push-Location (Join-Path $PSScriptRoot "..\..")

try {
    $out = Join-Path "artifacts" (Join-Path "windows" $Runtime)
    $publish = Join-Path $out "publish"
    $folder = Join-Path $out "ESA"
    $exe = Join-Path $folder "ESA.exe"
    $zip = Join-Path $out "ESA-$Version-$Runtime.zip"

    Write-Host "Publishing for $Runtime..."
    if (Test-Path $out) {
        Remove-Item $out -Recurse -Force
    }

    # Single file, with the native libraries inside it rather than beside it, and
    # compressed: about half the size, for a slightly slower first start.
    dotnet publish src/App.Ui/App.Ui.csproj -c Release -r $Runtime --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version `
        -o $publish
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    # Everything the publish left except the debug symbols: the executable, and the
    # files it opens from beside itself, which a single file cannot hold - today just
    # Help\User_Manual.pdf. ESA.ini, if the operator adds one, goes there too.
    New-Item -ItemType Directory $folder | Out-Null
    Get-ChildItem $publish |
        Where-Object { $_.Extension -ne ".pdb" } |
        Copy-Item -Destination $folder -Recurse

    # App.Ui is the assembly's name; ESA is the program's. Renaming a single-file
    # executable is safe, because it finds its contents inside itself, not by name.
    Rename-Item (Join-Path $folder "App.Ui.exe") "ESA.exe"

    Compress-Archive -Path $folder -DestinationPath $zip

    $megabytes = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    $zipMegabytes = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "Built $exe ($megabytes MB)"
    Write-Host "Zipped $zip ($zipMegabytes MB)"
}
finally {
    Pop-Location
}

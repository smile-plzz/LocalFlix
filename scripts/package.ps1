$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    dotnet publish src/LocalFlix.App/LocalFlix.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o dist/LocalFlix
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item README.md dist/LocalFlix/README.md
    Compress-Archive -Path dist/LocalFlix/* -DestinationPath dist/LocalFlix-windows-x64.zip -Force
    Write-Host 'Extract the entire ZIP before running LocalFlix.App.exe.'
} finally { Pop-Location }

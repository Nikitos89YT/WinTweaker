# Run with: dotnet run --project tests/CleanupPreview.Smoke.csproj
$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'CleanupPreview.Smoke.csproj')
if ($LASTEXITCODE -ne 0) { throw "Cleanup preview smoke test failed with exit code $LASTEXITCODE." }

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet run --project tests/DayPlanner.Tests/DayPlanner.Tests.csproj
if ($LASTEXITCODE -ne 0) { throw '截图检查失败' }
dotnet run --project tools/DayPlanner.Preview/DayPlanner.Preview.csproj
if ($LASTEXITCODE -ne 0) { throw '原生窗口截图失败' }
Copy-Item artifacts/avalonia-native/desktop-day.png docs/images/desktop.png -Force
dotnet run --project tools/DayPlanner.Preview/DayPlanner.Preview.csproj -- --mobile
if ($LASTEXITCODE -ne 0) { throw '手机布局截图失败' }
Copy-Item artifacts/avalonia-mobile/mobile-day.png docs/images/mobile.png -Force
Copy-Item artifacts/avalonia-mobile/mobile-week.png docs/images/mobile-week.png -Force
Write-Output '截图位于artifacts/avalonia-native和artifacts/avalonia-mobile。'

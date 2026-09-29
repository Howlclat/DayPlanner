param([ValidateSet('Windows','Android')][string]$Target = 'Windows', [string]$AndroidSdk = '', [string]$JavaSdk = '')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
if ($Target -eq 'Windows') {
    dotnet publish src/DayPlanner.Desktop/DayPlanner.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o release/windows
} else {
    if (-not $AndroidSdk) { $AndroidSdk = $env:ANDROID_HOME }
    if (-not $AndroidSdk) { $AndroidSdk = Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
    if (-not $JavaSdk) { $JavaSdk = $env:JAVA_HOME }
    if (-not $JavaSdk) { $JavaSdk = 'C:/Program Files/Android/Android Studio/jbr' }
    dotnet build src/DayPlanner.Android/DayPlanner.Android.csproj -c Debug "-p:AndroidSdkDirectory=$AndroidSdk" "-p:JavaSdkDirectory=$JavaSdk"
    if ($LASTEXITCODE -eq 0) {
        New-Item -ItemType Directory -Path release/android -Force | Out-Null
        Copy-Item -LiteralPath src/DayPlanner.Android/bin/Debug/net10.0-android/com.howlclat.dayplanner-Signed.apk -Destination release/android/DayPlanner-android-dev.apk -Force
    }
}
if ($LASTEXITCODE -ne 0) { throw '构建失败，请查看上方输出' }

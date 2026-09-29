param([string]$ProgramPath = 'release/DayPlanner.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ProgramPath).Path)

$output = Join-Path $projectRoot 'artifacts/readme-screenshots'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$today = [DateTime]::Today
$weekStart = $today.AddDays(-(([int]$today.DayOfWeek + 6) % 7))
$monthStart = [DateTime]::new($today.Year, $today.Month, 1)
$days = @{}
function Add-Sample([DateTime]$date, [string]$title, [int]$start, $end, [string]$color) {
    $key = $date.ToString('yyyy-MM-dd')
    if (-not $days.ContainsKey($key)) { $days[$key] = @() }
    $days[$key] += @{ Id = [Guid]::NewGuid().ToString(); Title = $title; Start = $start; End = $end; Color = $color }
}
for ($i = 0; $i -lt [DateTime]::DaysInMonth($today.Year, $today.Month); $i++) {
    $date = $monthStart.AddDays($i)
    if ($date.DayOfWeek -eq [DayOfWeek]::Monday) { Add-Sample $date '每周计划' 540 600 '#009DA4' }
    if ($date.DayOfWeek -eq [DayOfWeek]::Wednesday) { Add-Sample $date '方案评审' 840 930 '#428BD0' }
    if ($date.DayOfWeek -eq [DayOfWeek]::Friday) { Add-Sample $date '项目复盘' 900 960 '#009DA4' }
    if ($date.DayOfWeek -eq [DayOfWeek]::Saturday) { Add-Sample $date '户外运动' 480 570 '#E6A23A' }
}
$weekTasks = @('项目整理', '撰写方案', '资料整理', '产品设计', '整理笔记', '公园散步', '阅读学习')
for ($i = 0; $i -lt 7; $i++) {
    $date = $weekStart.AddDays($i)
    $days[$date.ToString('yyyy-MM-dd')] = @()
    Add-Sample $date $weekTasks[$i] (540 + ($i % 2) * 60) (660 + ($i % 2) * 60) '#009DA4'
    if ($i -lt 5) {
        Add-Sample $date '沟通讨论' 840 930 '#428BD0'
        Add-Sample $date '提交进展' 1020 $null '#7564F4'
    } else { Add-Sample $date '自由安排' 900 1020 '#E6A23A' }
}
$days[$today.ToString('yyyy-MM-dd')] = @()
Add-Sample $today '专注工作' 540 660 '#009DA4'
Add-Sample $today '午餐与休息' 720 780 '#E6A23A'
Add-Sample $today '项目讨论' 840 930 '#428BD0'
Add-Sample $today '整理资料' 960 1020 '#009DA4'
Add-Sample $today '回访客户' 1050 $null '#7564F4'
$dataPath = Join-Path $output 'sample-data.json'
@{ Version = 2; GridMinutes = 10; DefaultViewStart = 480; DefaultViewEnd = 1200; RememberCloseChoice = $true; CloseToTray = $false; Days = $days } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dataPath -Encoding UTF8

function Save-Visual($window, [string]$name) {
    $window.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int]$window.ActualWidth, [int]$window.ActualHeight, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($window)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $output $name))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    return $bitmap
}
function Set-View([string]$buttonName) {
    $window.FindName($buttonName).RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $window.FindName('ClockText').Text = '14:20:00'
    $window.FindName('Timeline').Now = $today.AddHours(14).AddMinutes(20)
    $window.FindName('WeekView').Tick($today.AddHours(14).AddMinutes(20))
    $window.UpdateLayout()
}

$app = [DayPlanner.App]::new()
$app.InitializeComponent()
$app.ShutdownMode = [Windows.ShutdownMode]::OnExplicitShutdown
$window = $null
$editor = $null
try {
    $window = [DayPlanner.MainWindow]::new($dataPath, $false)
    $window.Width = 1320; $window.Height = 840
    $window.Show()
    Set-View 'DayViewButton'
    $day = Save-Visual $window 'day.png'
    Set-View 'WeekViewButton'
    $week = Save-Visual $window 'week.png'
    Set-View 'MonthViewButton'
    $month = Save-Visual $window 'month.png'
    $item = [DayPlanner.ScheduleItem]::new()
    $item.Title = '回访客户'; $item.Start = 1050; $item.Color = '#7564F4'
    $item.ReminderEnabled = $true; $item.ReminderMinutes = 5
    $editor = [DayPlanner.EventEditor]::new($item, $true, $null, $today)
    $editor.Owner = $window
    $editor.Show()
    $editor.FindName('TitleInput').Select(0, 0)
    [void]$editor.FindName('SaveButton').Focus()
    $event = Save-Visual $editor 'new-event.png'

    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $sheetWidth = 2800; $sheetHeight = 1880
    $background = [Windows.Media.BrushConverter]::new().ConvertFromString('#EAF0F5')
    $ink = [Windows.Media.BrushConverter]::new().ConvertFromString('#203047')
    $muted = [Windows.Media.BrushConverter]::new().ConvertFromString('#738297')
    $dc.DrawRectangle($background, $null, [Windows.Rect]::new(0, 0, $sheetWidth, $sheetHeight))
    $pictures = @($day, $week, $month, $event)
    $titles = @('日视图', '周视图', '月视图', '新建事件')
    $captions = @('安排当天的时间点与时间段', '对比一周安排，拖动调整时间', '浏览整月计划，查看当天详情', '自定义颜色，设置提前提醒')
    for ($i = 0; $i -lt 4; $i++) {
        $x = 30 + ($i % 2) * 1390
        $y = 24 + [Math]::Floor($i / 2) * 932
        $panel = [Windows.Rect]::new($x, $y, 1360, 908)
        $dc.DrawRoundedRectangle([Windows.Media.Brushes]::White, $null, $panel, 14, 14)
        $title = [Windows.Media.FormattedText]::new($titles[$i], [Globalization.CultureInfo]::GetCultureInfo('zh-CN'), [Windows.FlowDirection]::LeftToRight, [Windows.Media.Typeface]::new('Microsoft YaHei UI'), 28, $ink)
        $dc.DrawText($title, [Windows.Point]::new($x + 24, $y + 16))
        $caption = [Windows.Media.FormattedText]::new($captions[$i], [Globalization.CultureInfo]::GetCultureInfo('zh-CN'), [Windows.FlowDirection]::LeftToRight, [Windows.Media.Typeface]::new('Microsoft YaHei UI'), 18, $muted)
        $dc.DrawText($caption, [Windows.Point]::new($x + 180, $y + 23))
        $image = $pictures[$i]
        $scale = [Math]::Min(1320 / $image.PixelWidth, 828 / $image.PixelHeight)
        $w = $image.PixelWidth * $scale; $h = $image.PixelHeight * $scale
        if ($i -eq 3) {
            $tint = [Windows.Media.BrushConverter]::new().ConvertFromString('#F4F7FA')
            $edge = [Windows.Media.BrushConverter]::new().ConvertFromString('#DCE4EA')
            $dc.DrawRoundedRectangle($tint, $null, [Windows.Rect]::new($x + 20, $y + 66, 1320, 828), 8, 8)
            $dc.DrawRoundedRectangle([Windows.Media.Brushes]::White, [Windows.Media.Pen]::new($edge, 1),
                [Windows.Rect]::new($x + (1360 - $w) / 2 - 1, $y + 66 + (828 - $h) / 2 - 1, $w + 2, $h + 2), 8, 8)
        }
        $dc.DrawImage($image, [Windows.Rect]::new($x + (1360 - $w) / 2, $y + 66 + (828 - $h) / 2, $w, $h))
    }
    $dc.Close()
    $sheet = [Windows.Media.Imaging.RenderTargetBitmap]::new($sheetWidth, $sheetHeight, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $sheet.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($sheet))
    $destination = Join-Path $projectRoot 'docs/images/planner-overview.png'
    $stream = [IO.File]::Create($destination)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Output $destination
} finally {
    if ($editor) { $editor.Close() }
    if ($window) { $window.Close() }
    $app.Shutdown()
}

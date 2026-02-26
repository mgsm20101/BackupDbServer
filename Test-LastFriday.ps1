# اختبار منطق آخر جمعة من الشهر
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "    اختبار منطق آخر جمعة من كل شهر" -ForegroundColor Yellow
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# دالة للحصول على آخر جمعة من الشهر
function Get-LastFridayOfMonth {
    param([DateTime]$date)
    
    # الحصول على آخر يوم في الشهر
    $lastDay = [DateTime]::new($date.Year, $date.Month, [DateTime]::DaysInMonth($date.Year, $date.Month))
    
    # إذا كان آخر يوم هو جمعة
    if ($lastDay.DayOfWeek -eq 'Friday') {
        return $lastDay
    }
    
    # نرجع للخلف حتى نجد جمعة
    $daysToSubtract = (([int]$lastDay.DayOfWeek - [int][DayOfWeek]::Friday + 7) % 7)
    return $lastDay.AddDays(-$daysToSubtract)
}

# اختبار لشهور مختلفة في 2025
$months = @(
    [DateTime]::new(2025, 1, 1)   # يناير
    [DateTime]::new(2025, 2, 1)   # فبراير
    [DateTime]::new(2025, 3, 1)   # مارس
    [DateTime]::new(2025, 4, 1)   # أبريل
    [DateTime]::new(2025, 5, 1)   # مايو
    [DateTime]::new(2025, 6, 1)   # يونيو
    [DateTime]::new(2025, 7, 1)   # يوليو
    [DateTime]::new(2025, 8, 1)   # أغسطس
    [DateTime]::new(2025, 9, 1)   # سبتمبر
    [DateTime]::new(2025, 10, 1)  # أكتوبر
    [DateTime]::new(2025, 11, 1)  # نوفمبر
    [DateTime]::new(2025, 12, 1)  # ديسمبر
)

Write-Host "📅 آخر جمعة من كل شهر في 2025:" -ForegroundColor Green
Write-Host ""

foreach ($month in $months) {
    $lastFriday = Get-LastFridayOfMonth $month
    $monthName = $month.ToString("MMMM", [System.Globalization.CultureInfo]::CreateSpecificCulture("ar-SA"))
    $lastDay = [DateTime]::new($month.Year, $month.Month, [DateTime]::DaysInMonth($month.Year, $month.Month))
    
    Write-Host "  $($month.ToString('yyyy-MM')) → " -NoNewline -ForegroundColor Cyan
    Write-Host "$($lastFriday.ToString('yyyy-MM-dd')) (الجمعة)" -NoNewline -ForegroundColor Yellow
    Write-Host " | آخر يوم: $($lastDay.ToString('yyyy-MM-dd')) ($($lastDay.DayOfWeek))" -ForegroundColor Gray
}

Write-Host ""
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ اختبار المنطق:" -ForegroundColor Green
Write-Host ""

# اختبار السيناريو: هل تاريخ معين هو آخر جمعة؟
$testDates = @(
    @{ Date = [DateTime]::new(2025, 1, 31); Expected = $true; Name = "31 يناير (آخر جمعة)" }
    @{ Date = [DateTime]::new(2025, 1, 24); Expected = $false; Name = "24 يناير (جمعة عادية)" }
    @{ Date = [DateTime]::new(2025, 2, 28); Expected = $true; Name = "28 فبراير (آخر جمعة)" }
    @{ Date = [DateTime]::new(2025, 12, 26); Expected = $true; Name = "26 ديسمبر (آخر جمعة)" }
)

foreach ($test in $testDates) {
    $lastFriday = Get-LastFridayOfMonth $test.Date
    $isLastFriday = ($test.Date.Date -eq $lastFriday.Date)
    
    $icon = if ($isLastFriday -eq $test.Expected) { "✅" } else { "❌" }
    $result = if ($isLastFriday) { "هو آخر جمعة" } else { "ليس آخر جمعة" }
    
    Write-Host "  $icon $($test.Name): " -NoNewline
    Write-Host "$result" -ForegroundColor $(if ($isLastFriday -eq $test.Expected) { "Green" } else { "Red" })
}

Write-Host ""
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📊 سياسة الاحتفاظ الجديدة:" -ForegroundColor Magenta
Write-Host ""
Write-Host "  1️⃣  آخر 7 أيام      → جميع النسخ اليومية" -ForegroundColor White
Write-Host "  2️⃣  آخر 4 أسابيع    → نسخة يوم الجمعة فقط" -ForegroundColor White
Write-Host "  3️⃣  آخر 12 شهر      → آخر جمعة من كل شهر" -ForegroundColor Yellow
Write-Host "  4️⃣  أقدم من سنة     → آخر جمعة من (يناير، أبريل، يوليو، أكتوبر)" -ForegroundColor Yellow
Write-Host ""
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan

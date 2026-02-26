# سكريبت تشغيل خدمة النسخ الاحتياطي بصلاحيات إدارية
# Run-Service.ps1

param(
    [switch]$AsAdmin
)

# التحقق من الصلاحيات الإدارية
function Test-IsAdmin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# إعادة التشغيل بصلاحيات إدارية
if (-not (Test-IsAdmin) -and -not $AsAdmin) {
    Write-Host "إعادة تشغيل السكريبت بصلاحيات إدارية..." -ForegroundColor Yellow
    
    $script = $MyInvocation.MyCommand.Path
    Start-Process -FilePath "powershell.exe" -ArgumentList "-File `"$script`" -AsAdmin" -Verb RunAs
    exit
}

Write-Host "تشغيل خدمة النسخ الاحتياطي..." -ForegroundColor Green
Write-Host "المجلد الحالي: $(Get-Location)" -ForegroundColor Cyan

try {
    # بناء التطبيق
    Write-Host "بناء التطبيق..." -ForegroundColor Yellow
    dotnet build BackupDbServer.csproj --configuration Release --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        throw "فشل بناء التطبيق"
    }
    
    # التحقق من وجود الملف التنفيذي
    $exePath = "bin\Release\net8.0-windows10.0.17763.0\BackupDbServer.exe"
    if (-not (Test-Path $exePath)) {
        throw "الملف التنفيذي غير موجود: $exePath"
    }
    
    Write-Host "تشغيل الخدمة..." -ForegroundColor Green
    Write-Host "استخدم Ctrl+C لإيقاف الخدمة" -ForegroundColor Cyan
    Write-Host ""
    
    # تشغيل الخدمة
    & $exePath --service
    
    Write-Host ""
    Write-Host "تم إيقاف الخدمة بنجاح" -ForegroundColor Green
}
catch {
    Write-Host "خطأ: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "اضغط أي زر للخروج..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

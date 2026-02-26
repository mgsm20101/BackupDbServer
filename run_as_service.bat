@echo off
echo بدء تشغيل خدمة النسخ الاحتياطي...
echo.
echo تحذير: يجب تشغيل هذا الملف بصلاحيات إدارية
echo المجلد الحالي: %CD%
echo.

REM التحقق من بناء التطبيق
echo بناء التطبيق...
dotnet build BackupDbServer.csproj --configuration Release

if %ERRORLEVEL% neq 0 (
    echo فشل بناء التطبيق!
    pause
    exit /b 1
)

REM تشغيل التطبيق كخدمة
echo تشغيل التطبيق كخدمة...
echo استخدم Ctrl+C لإيقاف الخدمة
echo.

REM التحقق من وجود المجلد والملف التنفيذي
if not exist "bin\Release\net8.0-windows10.0.17763.0" (
    echo خطأ: مجلد الإخراج غير موجود!
    echo تأكد من نجاح البناء أولاً
    pause
    exit /b 1
)

if not exist "bin\Release\net8.0-windows10.0.17763.0\BackupDbServer.exe" (
    echo خطأ: الملف التنفيذي غير موجود!
    echo تأكد من نجاح البناء أولاً
    pause
    exit /b 1
)

cd "bin\Release\net8.0-windows10.0.17763.0"
BackupDbServer.exe --service

echo.
echo تم إيقاف الخدمة
pause

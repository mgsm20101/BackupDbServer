# تعليمات نشر BackupDbServer (نسخة العميل)

> دليل موثّق وقابل للتكرار لإنتاج نسخة نشر جاهزة للتسليم لدى العميل.
> آخر تحديث: 2026-07-02 — الإصدار الحالي: **1.0.0**

---

## 1. نوع النسخة المعتمَد

| الخاصية | القيمة | السبب |
|---|---|---|
| Configuration | `Release` | نسخة مُحسّنة بدون رموز تصحيح |
| Runtime (RID) | `win-x64` | أجهزة/سيرفرات ويندوز 64-bit الحديثة |
| Self-contained | `true` | العميل **لا يحتاج تثبيت .NET 8** |
| Single-file | `true` | تسليم **ملف EXE واحد** فقط |
| ضغط | `true` | تقليل حجم الملف |
| صلاحيات | Administrator (من `app.manifest`) | عمليات SQL Server / IIS / خدمات ويندوز |

الناتج النهائي: **ملف واحد `BackupDbServer.exe` (~79 ميجا)**.

---

## 2. المتطلبات على جهاز المطوّر

- **.NET SDK 8** (أو أحدث) — تحقّق: `dotnet --version`
- ويندوز x64 (البناء يتم على نفس معمارية الـ RID للـ ReadyToRun لو فُعّل لاحقاً)
- استعادة الحزم تتم تلقائياً أثناء أول `publish`

---

## 3. خطوات إنتاج النسخة

### 3.1 أغلق أي نسخة عاملة من البرنامج
النسخة العاملة (خصوصاً Debug) تُقفل ملف الـ exe وتمنع البناء. أغلقها من الـ tray أو Task Manager.
> ملاحظة: النشر يكتب في `bin/Release` و`publish/` — لا يتعارض مع نسخة Debug العاملة، لكن يُفضّل إغلاقها.

### 3.2 (اختياري) حدّث رقم الإصدار
عدّل قيم `Version` / `FileVersion` في الأمر أدناه حسب الإصدار المطلوب.

### 3.3 نفّذ أمر النشر
من مجلد المشروع (`BackupDbServer/`):

```powershell
dotnet publish BackupDbServer.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:Version=1.0.0 -p:AssemblyVersion=1.0.0.0 -p:FileVersion=1.0.0.0 `
  -p:DebugType=none -p:DebugSymbols=false `
  -o "publish/ClientRelease-x64"
```

شرح المفاتيح المهمة:
- `IncludeNativeLibrariesForSelfExtract=true` — ضروري لتضمين المكتبات الأصلية (مثل SNI الخاص بـ `Microsoft.Data.SqlClient`) داخل الملف الواحد.
- `DebugType=none` — يمنع توليد ملف `.pdb` في نسخة التسليم.

### 3.4 تحقّق من الناتج
```powershell
Get-Item "publish/ClientRelease-x64/BackupDbServer.exe" | Select-Object Length
(Get-Item "publish/ClientRelease-x64/BackupDbServer.exe").VersionInfo | Select-Object FileVersion, ProductVersion
```
المتوقع: ملف واحد، `FileVersion = 1.0.0.0`.

---

## 4. محتوى حزمة التسليم

مجلد `publish/ClientRelease-x64/` يحتوي:
- `BackupDbServer.exe` — البرنامج (ملف واحد مستقل).
- `اقرأني-دليل-التشغيل.txt` — دليل التشغيل المختصر للعميل.

> **لا تُسلّم للعميل:** ملفات `DriveCredentials/` (رموز OAuth خاصة بجهازك)، ولا `settings.json`
> المحلي، ولا ملفات `*.log`. هذه تُنشأ تلقائياً على جهاز العميل عند أول تشغيل.

للإرسال: يكفي إرسال ملف الـ exe (أو ضغط المجلد كاملاً في ZIP إن رغبت بإرفاق الدليل).

---

## 5. التثبيت عند العميل

1. انسخ `BackupDbServer.exe` إلى أي مجلد على السيرفر.
2. شغّله (دبل كليك) ووافق على تنبيه صلاحية المسؤول (UAC).
3. من «الإعدادات» اضبط بيانات الاتصال بـ SQL Server واختبر الاتصال.
4. (اختياري) للتشغيل كخدمة ويندوز: `BackupDbServer.exe --service` من موجه أوامر بصلاحية مسؤول.

أماكن البيانات وقت التشغيل: `%APPDATA%\BackupDbServer\` (الإعدادات + `application.log`)،
والنسخ الاحتياطية في `Documents\{Local|Docker|Remote}Backups\`.

---

## 6. التحقق بعد النشر (Smoke Test)

على جهاز اختبار نظيف (يفضّل بدون .NET مثبّت للتأكد من الاستقلالية):
- [ ] البرنامج يفتح ويظهر تنبيه UAC ثم النافذة الرئيسية.
- [ ] فتح «إعدادات» واختبار اتصال SQL ينجح.
- [ ] فتح «إدارة المهام اليومية» بدون خطأ.
- [ ] إنشاء مهمة تكرار (كل N دقيقة) وحفظها؛ يظهر «الوقت المتبقي» يعدّ تنازلياً.
- [ ] مراجعة `%APPDATA%\BackupDbServer\application.log` وخلوّه من `[ERROR]`.

---

## 7. ملاحظات ومشاكل شائعة

- **نسخة واحدة فقط (Single Instance):** لا يعمل أكثر من نسخة في نفس الوقت (Mutex). تشغيل نسخة ثانية يُظهر نافذة النسخة العاملة.
- **فشل البناء «file is locked»:** نسخة عاملة تُقفل الـ exe — أغلقها ثم أعد النشر.
- **تحذير `WFAC010` (High DPI):** تحذير فقط ولا يمنع النشر.
- **معمارية أخرى:** لإنتاج نسخة 32-bit استبدل `-r win-x64` بـ `-r win-x86` (يوجد أيضاً Publish Profile قديم للـ x86 في `Properties/PublishProfiles/`).

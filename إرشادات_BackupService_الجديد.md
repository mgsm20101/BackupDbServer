# إرشادات BackupRestoreService الجديد

## 🎯 التحسينات المطبقة

تم تعديل `BackupRestoreService.cs` ليدعم جميع أنواع الاتصال (Docker، محلي، عن بُعد) برسائل ديناميكية واضحة.

## 🔄 التغييرات الرئيسية

### 1. الرسائل الديناميكية
- **قبل التعديل**: جميع الرسائل تشير إلى "حاوية Docker" حتى مع الاتصال المحلي
- **بعد التعديل**: رسائل مناسبة حسب نوع الاتصال:
  - `ConnectionType.Docker` → "حاوية Docker: {ContainerName}"
  - `ConnectionType.Local` → "خادم محلي: {ServerName}"
  - `ConnectionType.Remote` → "خادم عن بُعد: {ServerName}"

### 2. دالة `GetConnectionInfo()`
```csharp
private string GetConnectionInfo()
{
    return _settings.ConnectionType switch
    {
        ConnectionType.Docker => $"حاوية Docker: {_settings.DockerContainerName}",
        ConnectionType.Local => $"خادم محلي: {_settings.ServerName}",
        ConnectionType.Remote => $"خادم عن بُعد: {_settings.ServerName}",
        _ => $"خادم SQL Server: {_settings.ServerName}"
    };
}
```

### 3. النسخ الاحتياطي المنفصل
- **`HandleDockerBackupAsync()`**: معالجة خاصة بـ Docker (نسخ من/إلى الحاوية)
- **`HandleStandardBackupAsync()`**: معالجة للاتصال المحلي أو عن بُعد (نسخ مباشر)

### 4. الاستعادة المنفصلة
- **`HandleDockerRestoreAsync()`**: استعادة من حاوية Docker
- **`HandleStandardRestoreAsync()`**: استعادة للخادم المحلي أو عن بُعد

### 5. مجلدات النسخ الاحتياطي المتخصصة
```csharp
- Docker: "Documents/DockerBackups"
- Local: "Documents/LocalBackups"
- Remote: "Documents/RemoteBackups"
```

## 📋 الدوال الجديدة

| الدالة | الغرض | نوع الاتصال |
|-------|-------|-------------|
| `GetConnectionInfo()` | عرض معلومات الاتصال | جميع الأنواع |
| `HandleDockerBackupAsync()` | نسخ احتياطي Docker | Docker فقط |
| `HandleStandardBackupAsync()` | نسخ احتياطي عادي | Local/Remote |
| `VerifyStandardBackupAsync()` | التحقق من النسخة | Local/Remote |
| `HandleDockerRestoreAsync()` | استعادة Docker | Docker فقط |
| `HandleStandardRestoreAsync()` | استعادة عادية | Local/Remote |
| `GetStandardBackupFileInfoAsync()` | معلومات النسخة | Local/Remote |
| `PerformStandardRestoreAsync()` | تنفيذ الاستعادة | Local/Remote |

## 🔍 أمثلة على الرسائل الجديدة

### قبل التعديل:
```
تم العثور على 5 قاعدة بيانات في حاوية Docker
متصل بحاوية Docker: mycontainer
```

### بعد التعديل:
```
// للاتصال المحلي:
تم العثور على 5 قاعدة بيانات في خادم محلي: localhost
متصل بـ خادم محلي: localhost

// للاتصال عن بُعد:
تم العثور على 5 قاعدة بيانات في خادم عن بُعد: 192.168.1.100
متصل بـ خادم عن بُعد: 192.168.1.100

// لحاوية Docker:
تم العثور على 5 قاعدة بيانات في حاوية Docker: sqlserver2022
متصل بـ حاوية Docker: sqlserver2022
```

## ⚡ تدفق العمل الجديد

### النسخ الاحتياطي:
1. **تحديد نوع الاتصال** من `_settings.ConnectionType`
2. **Docker**: `HandleDockerBackupAsync()` (نسخ داخل الحاوية ثم إلى المحلي)
3. **Local/Remote**: `HandleStandardBackupAsync()` (نسخ مباشر إلى المجلد المحلي)

### الاستعادة:
1. **تحديد نوع الاتصال** من `_settings.ConnectionType`
2. **Docker**: `HandleDockerRestoreAsync()` (رفع إلى الحاوية ثم استعادة)
3. **Local/Remote**: `HandleStandardRestoreAsync()` (استعادة مباشرة)

## 🔧 نصائح للاستخدام

1. **تأكد من نوع الاتصال**: الكود يتصرف بشكل مختلف حسب `ConnectionType`
2. **مراقبة الرسائل**: الآن كل رسالة تعكس نوع الاتصال الفعلي
3. **مجلدات النسخ**: كل نوع اتصال له مجلد مخصص
4. **التحقق من التوافق**: Docker يتطلب معالجة خاصة (نسخ الملفات)

## 🎯 النتيجة

الآن `BackupRestoreService` يدعم:
- ✅ رسائل واضحة حسب نوع الاتصال
- ✅ معالجة منفصلة لكل نوع اتصال
- ✅ مجلدات نسخ احتياطي متخصصة
- ✅ تدفق عمل محسن للنسخ والاستعادة
- ✅ دعم كامل لـ Local/Remote/Docker

لا مزيد من الرسائل المضللة! 🎉

# 🎨 AirTun Brand & Visual Assets Hub

مجموعه کامل و آماده فایل‌های گرافیکی رسمی پروژه **AirTun (ایر‌تون)** بر پایه فایل‌های باکیفیت و شفاف (Transparent Master Assets).

---

## 📁 ساختار پوشه برندینگ (`C:\Tools\airtun\branding`)

```text
branding/
├── logoonly.png                 # امبلم و نماد اصلی ایر‌تون (شفاف - 1254x1254)
├── logo-with-text.png           # لوگوی کامل همراه با تایپوگرافی AirTun (شفاف - 1254x1254)
├── textonly.png                 # تایپوگرافی خالص AirTun (شفاف - 871x182)
├── transparent-white.png        # نسخه مونوکروم سفید شفاف (مخصوص نوتیفیکیشن‌ها و واترمارک)
├── logo-white.png               # نسخه سالید
├── github_banner.png            # بنر استاندارد هدر گیت‌هاب و شبکه‌های اجتماعی (1280x640)
├── github_avatar.png            # آواتار پروفایل و ریپازیتوری (500x500)
├── favicon.ico                  # فاوآیکون وبسایت و داکیومنت
│
├── windows/                     # خروجی‌های کلاینت ویندوز (WinUI 3)
│   ├── app.ico                  # آیکون ویندوز ترنسپرنت چندرزولوشن (16, 24, 32, 48, 64, 128, 256)
│   ├── TrayIcon.ico             # آیکون شفاف تسک‌بار و سیستم تری
│   ├── Square44x44Logo.png      # آیکون لیست اپلیکیشن‌های استارت منو
│   ├── Square150x150Logo.png    # تایل متوسط استارت منو
│   ├── Square310x310Logo.png    # تایل بزرگ استارت منو
│   ├── Wide310x150Logo.png      # تایل عریض استارت منو
│   ├── SplashScreen.png         # تصویر بارگذاری (Splash Screen)
│   └── StoreLogo.png            # نماد مایکروسافت استور
│
└── android/                     # خروجی‌های سرور اندروید
    ├── playstore-icon.png       # آیکون اصلی گوگل پلی استور (512x512)
    ├── mipmap-mdpi/             # 48x48 (ic_launcher.png, ic_launcher_round.png)
    ├── mipmap-hdpi/             # 72x72 (ic_launcher.png, ic_launcher_round.png)
    ├── mipmap-xhdpi/            # 96x96 (ic_launcher.png, ic_launcher_round.png)
    ├── mipmap-xxhdpi/           # 144x144 (ic_launcher.png, ic_launcher_round.png)
    └── mipmap-xxxhdpi/          # 192x192 (ic_launcher.png, ic_launcher_round.png)
```

---

## 💻 ۱. راهنمای استفاده در کلاینت ویندوز (`AirTun.App`)

فایل‌های مورد نیاز هم‌اکنون به صورت مستقیم در مسیر زیر قرار گرفته‌اند:  
`c:\Tools\airtun\windows\AirTun.App\Assets\`

### تنظیم آیکون فایل اجرایی در `AirTun.App.csproj`:
```xml
<PropertyGroup>
  <ApplicationIcon>Assets\app.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <Content Include="Assets\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### تنظیم آیکون پنجره و Tray در `MainWindow.xaml.cs`:
```csharp
// در متد سازنده پنجره:
IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
appWindow.SetIcon(@"Assets\app.ico");
```

---

## 📱 ۲. راهنمای استفاده در سرور اندروید (`android`)

فایل‌های mipmap و drawable مستقیماً در مسیر منابع پروژه اندروید تزریق شده‌اند:  
`c:\Tools\airtun\android\app\src\main\res\`

### تنظیم در `AndroidManifest.xml`:
```xml
<application
    android:icon="@mipmap/ic_launcher"
    android:roundIcon="@mipmap/ic_launcher_round"
    android:label="@string/app_name"
    android:theme="@style/Theme.AirTun">
    ...
</application>
```

### استفاده برای نوتیفیکیشن سرویس فورگراند در کاتلین:
```kotlin
val notification = NotificationCompat.Builder(context, CHANNEL_ID)
    .setSmallIcon(R.drawable.ic_notification) // آیکون سفید مونوکروم
    .setContentTitle("AirTun Server Active")
    .setContentText("SOCKS5 Engine running on :10808")
    ...
    .build()
```

---

## 🌐 ۳. راهنمای استفاده در GitHub README

برای نمایش شیک لوگوی ترنسپرنت در هدر مخزن گیت‌هاب:

```markdown
<div align="center">
  <img src="branding/logo-with-text.png" width="180" alt="AirTun Logo" />
  <h1>⚡ AirTun (ایر‌تون)</h1>
  <p><b>Ultra-Fast, Low-Latency Phone Internet Sharing for Windows</b></p>
</div>
```

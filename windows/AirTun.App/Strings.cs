namespace AirTun.App;

public static class Strings
{
    private static bool _isPersian = false;

    public static bool IsPersian
    {
        get => _isPersian;
        set => _isPersian = value;
    }

    public static string FlowDirection => _isPersian ? "RightToLeft" : "LeftToRight";

    public static string AppName => "AirTun";
    public static string Tagline => _isPersian ? "اشتراک بدون مرز اینترنت گوشی با ویندوز" : "Ultra-Fast Phone Internet Sharing";

    // Bottom Navigation Tabs
    public static string TabConnect => _isPersian ? "اتصال" : "Connect";
    public static string TabRouting => _isPersian ? "مسیریابی" : "Routing";
    public static string TabLogs => _isPersian ? "لاگ‌ها" : "Logs";
    public static string TabAbout => _isPersian ? "تنظیمات و درباره" : "Settings & About";

    // Statuses
    public static string StatusIdle => _isPersian ? "آماده برای اتصال" : "Ready to Connect";
    public static string StatusPreparing => _isPersian ? "در حال ایجاد تونل..." : "Starting Tunnel...";
    public static string StatusConnected => _isPersian ? "اتصال برقرار است" : "Connected & Protected";
    public static string StatusDisconnected => _isPersian ? "قطع شد" : "Disconnected";
    public static string StatusError => _isPersian ? "خطا در اتصال" : "Connection Error";

    // Modes
    public static string ModeTunTitle => _isPersian ? "تونل کامل سیستم (TUN)" : "Full System TUN";
    public static string ModeTunSubtitle => _isPersian ? "گیمینگ، تلگرام و همه برنامه‌ها" : "Games, Telegram & All Apps";
    public static string ModeProxyTitle => _isPersian ? "پروکسی وب (Proxy)" : "Web Proxy Mode";
    public static string ModeProxySubtitle => _isPersian ? "مرورگرها و ترافیک وب" : "Browsers & HTTP Apps";

    // Routing & Bypass Domestic
    public static string RoutingTitle => _isPersian ? "مسیریابی هوشمند و قوانین" : "Smart Routing Rules";
    public static string BypassDomesticTitle => _isPersian ? "دایرکت سایت‌های ایرانی (.ir)" : "Bypass Iranian Sites (.ir)";
    public static string BypassDomesticDesc => _isPersian
        ? "سایت‌ها، بانک‌ها و سامانه‌های داخلی بدون عبور از تونل مستقیماً باز می‌شوند."
        : "Domestic (.ir) domains & banks connect directly without tunneling for speed and savings.";

    public static string CustomRulesHeader => _isPersian ? "قوانین سفارشی روتینگ" : "Custom Routing Rules";
    public static string AddRuleAction => _isPersian ? "+ افزودن قانون" : "+ Add Rule";
    public static string RulePatternPlaceholder => _isPersian ? "دامنه (مثال: digikala.com یا *.ir)" : "Domain (e.g. digikala.com or *.ir)";
    public static string RuleActionDirect => _isPersian ? "مستقیم" : "Direct";
    public static string RuleActionProxy => _isPersian ? "از تونل" : "Proxy";
    public static string RuleActionBlock => _isPersian ? "مسدود" : "Block";
    public static string DeleteRuleAction => _isPersian ? "حذف" : "Delete";

    // Discovery & Connect
    public static string DiscoveredHeader => _isPersian ? "گوشی در دسترس" : "Available Device";
    public static string SearchingDevices => _isPersian ? "در حال جستجو برای گوشی..." : "Searching for Phone...";
    public static string PinHint => _isPersian ? "پین ۴ رقمی روی صفحه گوشی را وارد کنید" : "Enter the 4-digit PIN on phone screen";
    public static string ConnectAction => _isPersian ? "اتصال به گوشی" : "Connect to Phone";
    public static string DisconnectAction => _isPersian ? "قطع ارتباط" : "Disconnect";
    public static string RetryAction => _isPersian ? "تلاش مجدد" : "Retry";
    public static string DismissAction => _isPersian ? "بستن" : "Dismiss";

    // Dashboard
    public static string TrafficHeader => _isPersian ? "ترافیک و سرعت لحظه‌ای" : "Live Network Bandwidth";
    public static string LiveTrafficHeader => _isPersian ? "نمودار زنده پهنای باند" : "Live Traffic Waveform";
    public static string TrafficTotal => _isPersian ? "حجم کل" : "Total Data";
    public static string LatencyLabel => _isPersian ? "پینگ" : "Latency";
    public static string DurationLabel => _isPersian ? "زمان" : "Duration";
    public static string OutboundIpHeader => _isPersian ? "لوکیشن و آی‌پی خروجی" : "Outbound Location";
    public static string FetchingGeo => _isPersian ? "در حال استعلام لوکیشن..." : "Resolving outbound location...";
    public static string RefreshGeoAction => _isPersian ? "بروزرسانی" : "Refresh";

    // Logs
    public static string LogsHeader => _isPersian ? "لاگ‌های سیستم" : "System Diagnostics & Logs";
    public static string CopyLogsAction => _isPersian ? "کپی لاگ‌ها" : "Copy Logs";
    public static string ClearLogsAction => _isPersian ? "پاک‌سازی" : "Clear";

    // Tray & Startup Settings
    public static string SettingsHeader => _isPersian ? "تنظیمات برنامه" : "App Behavior & Tray";
    public static string StartWithWindowsTitle => _isPersian ? "اجرا همراه با ویندوز در System Tray" : "Start with Windows (System Tray)";
    public static string StartWithWindowsDesc => _isPersian ? "اجرای خودکار برنامه هنگام روشن شدن سیستم به صورت سایلنت در تسک‌بار." : "Launch automatically on Windows startup minimized to system tray.";
    public static string CloseToTrayTitle => _isPersian ? "انتقال به System Tray با زدن ✕" : "Minimize to Tray on Close (X)";
    public static string CloseToTrayDesc => _isPersian ? "پنجره بسته می‌شود اما اتصال در پس‌زمینه فعال می‌ماند." : "Keep connection active in background when window is closed.";
    public static string MinimizeToTrayTitle => _isPersian ? "مخفی‌سازی به Tray با زدن کمینه (_)" : "Minimize to Tray on Minimize (_)";
    public static string MinimizeToTrayDesc => _isPersian ? "پنجره هنگام مینیمایز شدن در تسک‌بار مخفی می‌شود." : "Hide window to system tray when minimized.";

    // About
    public static string AboutTitle => _isPersian ? "درباره ایر‌تون" : "About AirTun";
    public static string AboutDescription => _isPersian
        ? "نرم‌افزار مدرن، فوق سریع و امن برای اشتراک‌گذاری اینترنت گوشی با سیستم‌های ویندوزی بدون محدودیت اپراتور با پینگ بهینه برای گیمینگ و وبگردی."
        : "Ultra-fast, low-latency and secure phone internet sharing for Windows systems with zero operator restrictions, tailored for online gaming and daily workflows.";
    public static string OpenGithubAction => _isPersian ? "مشاهده پروژه در گیت‌هاب" : "Open Project on GitHub";
    public static string DeveloperTitle => _isPersian ? "توسعه‌دهنده:" : "Developer:";
    public static string DeveloperName => "Omid Zaferi";
    public static string LicenseTitle => _isPersian ? "مجوز:" : "License:";
    public static string LicenseName => "MIT License";

    // Tray Context Menu
    public static string TrayOpen => _isPersian ? "باز کردن AirTun" : "Open AirTun";
    public static string TrayExit => _isPersian ? "خروج کامل" : "Exit AirTun";

    // Errors
    public static string GetErrorTitle(string? code) => code switch
    {
        "ERR_INVALID_PIN" => _isPersian ? "پین‌کد نادرست است" : "Invalid Security PIN",
        "ERR_ELEVATION_DECLINED" => _isPersian ? "دسترسی ادمین تایید نشد" : "Admin Permission Declined",
        "ERR_TUNNEL_START_FAILED" => _isPersian ? "خطا در ساخت کارت شبکه" : "Adapter Setup Failed",
        "ERR_CONNECTION_REFUSED" => _isPersian ? "عدم پاسخ سرور گوشی" : "Phone Unreachable",
        "ERR_PROXY_APPLY_FAILED" => _isPersian ? "خطا در پروکسی ویندوز" : "Proxy Setup Failed",
        _ => _isPersian ? "خطا در اتصال" : "Connection Error",
    };

    public static string GetErrorBody(string? code) => code switch
    {
        "ERR_INVALID_PIN" => _isPersian
            ? "پین‌کد ۴ رقمی وارد شده با پین گوشی مطابقت ندارد."
            : "The 4-digit PIN entered does not match the PIN on the phone screen.",
        "ERR_ELEVATION_DECLINED" => _isPersian
            ? "برای ساخت کارت شبکه WinTun، دسترسی ادمین ویندوز الزامی است."
            : "Administrator privilege is required by Windows to create the virtual network adapter.",
        "ERR_TUNNEL_START_FAILED" => _isPersian
            ? "امکان راه‌اندازی کارت شبکه میسر نشد. سایر برنامه‌های VPN را ببندید و مجدداً تلاش کنید."
            : "Could not create the WinTun adapter. Please close conflicting VPNs and retry.",
        "ERR_CONNECTION_REFUSED" => _isPersian
            ? "گوشی پاسخ نداد. بررسی کنید هات‌اسپات روشن باشد و دکمه Start در اپ گوشی فعال باشد."
            : "Cannot reach the Android device. Ensure the hotspot is active and AirTun is started on your phone.",
        _ => _isPersian
            ? "ارتباط با مشکل مواجه شد. لطفاً وضعیت هات‌اسپات و پین‌کد را بررسی کرده و دوباره امتحان کنید."
            : "An unexpected error occurred. Please verify your hotspot connection and try again.",
    };
}

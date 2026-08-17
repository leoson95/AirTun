namespace AirTun.App;

public static class Strings
{
    private static bool _isPersian;

    public static bool IsPersian
    {
        get => _isPersian;
        set => _isPersian = value;
    }

    public static string FlowDirection => _isPersian ? "RightToLeft" : "LeftToRight";

    public static string AppName => "AirTun";
    public static string Tagline => _isPersian ? "اشتراک بدون مرز اینترنت گوشی با ویندوز" : "Seamless Phone Internet Sharing";

    public static string StatusIdle => _isPersian ? "آماده اتصال" : "Ready to Connect";
    public static string StatusPreparing => _isPersian ? "در حال برقراری ارتباط و ساخت کارت شبکه..." : "Setting up connection & adapter...";
    public static string StatusConnected => _isPersian ? "متصل (ترافیک در حال عبور)" : "Connected (Traffic Active)";
    public static string StatusDisconnected => _isPersian ? "ارتباط قطع شد" : "Disconnected";
    public static string StatusError => _isPersian ? "خطا در اتصال" : "Connection Error";

    public static string ModeFullTun => _isPersian ? "حالت کامل تونل سیستم (Full TUN Mode)" : "Full System TUN Mode";
    public static string ModeFastProxy => _isPersian ? "حالت پروکسی سریع وب (Fast Web Proxy)" : "Fast Web Proxy Mode";
    public static string ModeDescTun => _isPersian
        ? "مسیریابی ۱۰۰٪ ترافیک ویندوز از طریق کارت شبکه مجازی (پوشش کامل بازی‌ها، تلگرام، گیت، ترمینال و وب)"
        : "Routes 100% of Windows traffic through virtual adapter (Gaming, Telegram, Git, Docker, Browsers)";
    public static string ModeDescProxy => _isPersian
        ? "تنظیم پروکسی سیستمی ویندوز برای مرورگرها و برنامه‌های وبگردی"
        : "Configures Windows system proxy for browsers and standard HTTP/HTTPS apps";

    public static string DiscoveredDevices => _isPersian ? "دستگاه‌های کشف‌شده در هات‌اسپات / وای‌فای" : "Discovered Android Devices";
    public static string SearchingDevices => _isPersian ? "در حال کاوش شبکه محلی جهت یافتن گوشی..." : "Searching local Wi-Fi / Hotspot for AirTun...";
    public static string ManualConnect => _isPersian ? "یا اتصال دستی با آدرس آی‌پی" : "Or Connect Manually by IP";

    public static string PinLabel => _isPersian ? "کد پین ۴ رقمی امنیتی" : "4-Digit Security PIN";
    public static string PinHint => _isPersian ? "پین ۴ رقمی نمایش‌داده‌شده روی صفحه گوشی را وارد کنید" : "Enter the 4-digit PIN displayed on your Android phone";
    public static string HostLabel => _isPersian ? "آدرس آی‌پی سرور" : "Server Host / IP";

    public static string ConnectAction => _isPersian ? "اتصال" : "Connect";
    public static string DisconnectAction => _isPersian ? "قطع ارتباط" : "Disconnect";
    public static string CancelAction => _isPersian ? "انصراف" : "Cancel";
    public static string RetryAction => _isPersian ? "تلاش مجدد" : "Retry";
    public static string DismissAction => _isPersian ? "بستن" : "Dismiss";

    public static string TrafficHeader => _isPersian ? "ترافیک و سرعت لحظه‌ای" : "Live Traffic & Bandwidth";
    public static string TrafficTotal => _isPersian ? "حجم کل مبادله‌شده" : "Total Transferred";
    public static string SpeedRate => _isPersian ? "سرعت انتقال" : "Current Rate";
    public static string LatencyLabel => _isPersian ? "پینگ به گوشی" : "Tunnel Latency";
    public static string DurationLabel => _isPersian ? "مدت اتصال" : "Duration";

    public static string AdvancedSection => _isPersian ? "تنظیمات و گزارش فنی" : "Advanced & Diagnostics";
    public static string LanguageLabel => _isPersian ? "زبان برنامه (Language)" : "Language";
    public static string ThemeLabel => _isPersian ? "تم ظاهری (Theme)" : "Theme";
    public static string ThemeDark => _isPersian ? "تیره (Dark)" : "Dark";
    public static string ThemeLight => _isPersian ? "روشن (Light)" : "Light";
    public static string ThemeSystem => _isPersian ? "سیستم (System)" : "System";
    public static string CopyLogsAction => _isPersian ? "کپی لاگ‌ها" : "Copy Logs";
    public static string ClearLogsAction => _isPersian ? "پاک‌سازی" : "Clear";
    public static string NoLogs => _isPersian ? "هیچ لاگی ثبت نشده است." : "No log entries yet.";

    public static string TrayOpen => _isPersian ? "باز کردن پنجره AirTun" : "Open AirTun";
    public static string TrayExit => _isPersian ? "خروج کامل" : "Exit AirTun";

    public static string GetErrorTitle(string? code) => code switch
    {
        "ERR_INVALID_PIN" => _isPersian ? "پین‌کد نادرست است" : "Invalid Security PIN",
        "ERR_ELEVATION_DECLINED" => _isPersian ? "دسترسی ادمین تایید نشد" : "Administrator Permission Declined",
        "ERR_TUNNEL_START_FAILED" => _isPersian ? "خطا در راه‌اندازی کارت شبکه" : "Failed to Start Virtual Adapter",
        "ERR_CONNECTION_REFUSED" => _isPersian ? "عدم پاسخ سرور اندروید" : "Android Server Unreachable",
        "ERR_PROXY_APPLY_FAILED" => _isPersian ? "خطا در اعمال پروکسی سیستم" : "Failed to Apply System Proxy",
        _ => _isPersian ? "خطا در اتصال" : "Connection Error",
    };

    public static string GetErrorBody(string? code) => code switch
    {
        "ERR_INVALID_PIN" => _isPersian
            ? "پین‌کد ۴ رقمی وارد شده با پین نمایش داده شده روی گوشی مطابقت ندارد."
            : "The 4-digit PIN entered does not match the PIN on the phone screen.",
        "ERR_ELEVATION_DECLINED" => _isPersian
            ? "برای ساخت کارت شبکه مجازی WinTun و مسیریابی بازی‌ها، دسترسی ادمین مورد نیاز است."
            : "Administrator privilege is required by Windows to create the WinTun virtual adapter.",
        "ERR_TUNNEL_START_FAILED" => _isPersian
            ? "امکان ایجاد اینترفیس مجازی WinTun میسر نشد. لطفاً برنامه‌های VPN دیگر را ببندید و مجدداً امتحان کنید."
            : "Could not create the WinTun adapter. Please close any conflicting VPN software and retry.",
        "ERR_CONNECTION_REFUSED" => _isPersian
            ? "اتصال به گوشی برقرار نشد. اطمینان حاصل کنید هات‌اسپات روشن است و دکمه شروع در برنامه گوشی فعال می‌باشد."
            : "Cannot reach the Android device. Ensure the hotspot is active and AirTun is started on your phone.",
        _ => _isPersian
            ? "ارتباط با مشکل مواجه شد. لطفاً وضعیت هات‌اسپات و پین‌کد را بررسی نموده و دوباره تلاش کنید."
            : "An unexpected error occurred. Please verify your hotspot connection and try again.",
    };
}

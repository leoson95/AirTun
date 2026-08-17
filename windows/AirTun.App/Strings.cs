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
    public static string Tagline => _isPersian ? "اشتراک بدون مرز اینترنت گوشی با ویندوز" : "Seamless Phone Internet Sharing";

    public static string StatusIdle => _isPersian ? "آماده اتصال" : "Ready to Connect";
    public static string StatusPreparing => _isPersian ? "در حال ساخت کارت شبکه و اتصال..." : "Setting up connection & adapter...";
    public static string StatusConnected => _isPersian ? "متصل (ترافیک فعال)" : "Connected (Traffic Active)";
    public static string StatusDisconnected => _isPersian ? "ارتباط قطع شد" : "Disconnected";
    public static string StatusError => _isPersian ? "خطا در اتصال" : "Connection Error";

    public static string ModeTunTitle => _isPersian ? "تونل کامل سیستم (TUN)" : "Full TUN Mode";
    public static string ModeTunSubtitle => _isPersian ? "گیمینگ، تلگرام و همه برنامه‌ها" : "Games, Telegram, CLI & All Apps";
    public static string ModeProxyTitle => _isPersian ? "پروکسی وب (Proxy)" : "Web Proxy Mode";
    public static string ModeProxySubtitle => _isPersian ? "مرورگرها و وب‌گردی سبک" : "Browsers & HTTP Apps";

    public static string ModeDescTun => _isPersian
        ? "مسیریابی ۱۰۰٪ ترافیک ویندوز از طریق کارت شبکه مجازی (پوشش بازی‌ها، تلگرام، گیت، داکر و وب)"
        : "Routes 100% of Windows traffic through virtual adapter (Gaming, Telegram, Git, Docker, Browsers)";
    public static string ModeDescProxy => _isPersian
        ? "تنظیم پروکسی سیستمی ویندوز برای مرورگرها و برنامه‌های وبگردی"
        : "Configures Windows system proxy for browsers and standard HTTP/HTTPS apps";

    public static string RoutingHeader => _isPersian ? "کنترل مسیریابی و قوانین" : "Smart Routing & Rules";
    public static string BypassDomesticTitle => _isPersian ? "دایرکت ترافیک ایران (.ir)" : "Bypass Iranian Sites (.ir)";
    public static string BypassDomesticDesc => _isPersian
        ? "سایت‌ها، بانک‌ها و اپ‌های ایرانی بدون عبور از فیلترشکن مستقیماً باز می‌شوند تا ترافیک مصرف نشود و درگاه‌های پرداخت قطع نشوند."
        : "Iranian (.ir) domains & banking services connect directly without tunneling for max speed and savings.";

    public static string CustomRulesHeader => _isPersian ? "قوانین سفارشی روتینگ" : "Custom Routing Rules";
    public static string CustomRulesTooltip => _isPersian
        ? "تعریف قوانین اختصاصی بر اساس دامنه یا آی‌پی (مشابه Geosite در وی‌توری)"
        : "Define custom routing rules for specific domains or IPs (Direct, Proxy, Block)";

    public static string AddRuleAction => _isPersian ? "افزودن" : "Add";
    public static string RulePatternPlaceholder => _isPersian ? "دامنه (مثال: digikala.com یا *.ir)" : "Domain (e.g. google.com or *.ir)";
    public static string RuleActionDirect => _isPersian ? "مستقیم" : "Direct";
    public static string RuleActionProxy => _isPersian ? "تونل" : "Proxy";
    public static string RuleActionBlock => _isPersian ? "مسدود" : "Block";
    public static string DeleteRuleAction => _isPersian ? "حذف" : "Delete";

    public static string OutboundIpHeader => _isPersian ? "آی‌پی و موقعیت خروجی" : "Outbound Location";
    public static string FetchingGeo => _isPersian ? "در حال دریافت لوکیشن..." : "Resolving outbound location...";
    public static string RefreshGeoAction => _isPersian ? "بروزرسانی" : "Refresh";

    public static string DiscoveredDevices => _isPersian ? "گوشی‌های شناسایی‌شده" : "Discovered Devices";
    public static string SearchingDevices => _isPersian ? "در حال جستجو در هات‌اسپات / وای‌فای..." : "Searching local Wi-Fi / Hotspot for AirTun...";
    public static string ManualConnect => _isPersian ? "اتصال دستی با آی‌پی" : "Manual IP Connection";

    public static string PinLabel => _isPersian ? "پین‌کد ۴ رقمی" : "4-Digit PIN";
    public static string PinHint => _isPersian ? "پین ۴ رقمی روی صفحه گوشی را وارد کنید" : "Enter the 4-digit PIN displayed on your Android phone";
    public static string HostLabel => _isPersian ? "آدرس آی‌پی سرور" : "Server IP Address";

    public static string ConnectAction => _isPersian ? "اتصال به گوشی" : "Connect";
    public static string DisconnectAction => _isPersian ? "قطع ارتباط" : "Disconnect";
    public static string CancelAction => _isPersian ? "انصراف" : "Cancel";
    public static string RetryAction => _isPersian ? "تلاش مجدد" : "Retry";
    public static string DismissAction => _isPersian ? "بستن" : "Dismiss";

    public static string TrafficHeader => _isPersian ? "سرعت و حجم مصرفی" : "Bandwidth & Traffic";
    public static string TrafficTotal => _isPersian ? "حجم کل" : "Total Data";
    public static string LatencyLabel => _isPersian ? "پینگ" : "Latency";
    public static string DurationLabel => _isPersian ? "زمان" : "Duration";

    public static string AdvancedSection => _isPersian ? "تنظیمات پیشرفته و لاگ‌ها" : "Advanced & Logs";
    public static string CopyLogsAction => _isPersian ? "کپی" : "Copy";
    public static string ClearLogsAction => _isPersian ? "پاک‌سازی" : "Clear";

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

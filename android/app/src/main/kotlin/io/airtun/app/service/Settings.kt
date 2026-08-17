package io.airtun.app.service

import android.content.Context
import android.content.SharedPreferences

class Settings(context: Context) {
    private val prefs: SharedPreferences =
        context.getSharedPreferences("airtun_settings", Context.MODE_PRIVATE)

    var themeMode: String
        get() = prefs.getString("theme_mode", "system") ?: "system"
        set(value) = prefs.edit().putString("theme_mode", value).apply()
}

package io.airtun.app.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

data class GlassColors(
    val backgroundBase: Color,
    val backgroundGradientTop: Color,
    val backgroundGradientBottom: Color,
    val fill: Color,
    val fillRaised: Color,
    val stroke: Color,
    val strokeHighlight: Color,
    val textPrimary: Color,
    val textSecondary: Color,
    val textTertiary: Color,
    val accent: Color,
    val accentPressed: Color,
    val accentSubtle: Color,
    val error: Color,
    val errorSubtle: Color,
    val warning: Color,
    val onAccent: Color,
    val isDark: Boolean,
) {
    val radiusSm = 12.dp
    val radiusMd = 16.dp
    val radiusLg = 24.dp
}

private val DarkGlass = GlassColors(
    backgroundBase = Color(0xFF0B0E14),
    backgroundGradientTop = Color(0xFF10141D),
    backgroundGradientBottom = Color(0xFF080B0F),
    fill = Color(0xFF10141D),
    fillRaised = Color(0xFF181D28),
    stroke = Color(0xFF262F3F),
    strokeHighlight = Color(0xFF262F3F),
    textPrimary = Color(0xFFF1F5F9),
    textSecondary = Color(0xFF94A3B8),
    textTertiary = Color(0xFF64748B),
    accent = Color(0xFF00E5FF),
    accentPressed = Color(0xFF00B8D4),
    accentSubtle = Color(0x2900E5FF),
    error = Color(0xFFFF3366),
    errorSubtle = Color(0x29FF3366),
    warning = Color(0xFFFFB703),
    onAccent = Color(0xFF000000),
    isDark = true,
)

private val LightGlass = GlassColors(
    backgroundBase = Color(0xFFF0F4F8),
    backgroundGradientTop = Color(0xFFF8FAFD),
    backgroundGradientBottom = Color(0xFFE4E9F0),
    fill = Color.White.copy(alpha = 0.65f),
    fillRaised = Color.White.copy(alpha = 0.85f),
    stroke = Color.Black.copy(alpha = 0.08f),
    strokeHighlight = Color.White.copy(alpha = 0.90f),
    textPrimary = Color(0xFF0C1018).copy(alpha = 0.94f),
    textSecondary = Color(0xFF0C1018).copy(alpha = 0.60f),
    textTertiary = Color(0xFF0C1018).copy(alpha = 0.45f),
    accent = Color(0xFF00838F),
    accentPressed = Color(0xFF006064),
    accentSubtle = Color(0x2900838F),
    error = Color(0xFFD32F2F),
    errorSubtle = Color(0x29D32F2F),
    warning = Color(0xFFF57C00),
    onAccent = Color(0xFFFFFFFF),
    isDark = false,
)

val LocalGlass = staticCompositionLocalOf { DarkGlass }

private val glassTypography = Typography(
    displaySmall = TextStyle(fontSize = 32.sp, lineHeight = 38.sp, fontWeight = FontWeight.Bold),
    titleLarge = TextStyle(fontSize = 22.sp, lineHeight = 28.sp, fontWeight = FontWeight.SemiBold),
    bodyMedium = TextStyle(fontSize = 15.sp, lineHeight = 22.sp, fontWeight = FontWeight.Normal),
    labelSmall = TextStyle(fontSize = 12.sp, lineHeight = 16.sp, fontWeight = FontWeight.Medium),
    headlineMedium = TextStyle(
        fontSize = 34.sp, lineHeight = 42.sp, fontWeight = FontWeight.Bold, letterSpacing = 6.sp,
    ),
)

@Composable
fun AirTunTheme(themeMode: String = "system", content: @Composable () -> Unit) {
    val dark = when (themeMode) {
        "dark" -> true
        "light" -> false
        else -> isSystemInDarkTheme()
    }
    val glass = if (dark) DarkGlass else LightGlass

    val view = LocalView.current
    if (!view.isInEditMode) {
        (view.context as? Activity)?.window?.let { window ->
            SideEffect {
                WindowCompat.getInsetsController(window, view).apply {
                    isAppearanceLightStatusBars = !dark
                    isAppearanceLightNavigationBars = !dark
                }
            }
        }
    }

    val colorScheme = if (dark) {
        darkColorScheme(
            primary = glass.accent,
            background = glass.backgroundBase,
            onBackground = glass.textPrimary,
            surface = glass.backgroundBase,
            onSurface = glass.textPrimary,
            error = glass.error,
        )
    } else {
        lightColorScheme(
            primary = glass.accent,
            background = glass.backgroundBase,
            onBackground = glass.textPrimary,
            surface = glass.backgroundBase,
            onSurface = glass.textPrimary,
            error = glass.error,
        )
    }
    CompositionLocalProvider(LocalGlass provides glass) {
        MaterialTheme(colorScheme = colorScheme, typography = glassTypography, content = content)
    }
}

package io.airtun.app.ui.theme

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

fun Modifier.glassPanel(radius: Dp = 20.dp, raised: Boolean = false): Modifier = nmCard(radius)

/**
 * Neumorphic RAISED card — matches HTML:
 *   background: linear-gradient(145deg, #1c2232, #10131d)
 *   box-shadow: 8px 8px 18px rgba(0,0,0,0.55), -5px -5px 14px rgba(255,255,255,0.04)
 *   border: 1px solid rgba(255,255,255,0.07) / rgba(0,0,0,0.65)
 */
fun Modifier.nmCard(radius: Dp = 20.dp): Modifier = composed {
    val shape = RoundedCornerShape(radius)
    this
        // Directional elevation shadow (dark bottom-right, matches CSS box-shadow positive offsets)
        .shadow(
            elevation = 8.dp,
            shape = shape,
            clip = false,
            spotColor = Color(0x8C000000),    // 55% black — matches rgba(0,0,0,0.55)
            ambientColor = Color(0x33000000), // 20% black ambient
        )
        .clip(shape)
        // Raised gradient: lighter top-left → darker bottom-right
        .background(
            Brush.linearGradient(
                colors = listOf(Color(0xFF1C2232), Color(0xFF10131D)),
                start = Offset.Zero,
                end = Offset.Infinite,
            )
        )
        // Asymmetric border: light top-left edge / dark bottom-right edge
        .border(
            width = 1.dp,
            brush = Brush.linearGradient(
                colors = listOf(
                    Color(0x12FFFFFF), // top-left: rgba(255,255,255,0.07) — light edge
                    Color(0xA6000000), // bottom-right: rgba(0,0,0,0.65) — dark edge
                ),
                start = Offset.Zero,
                end = Offset.Infinite,
            ),
            shape = shape,
        )
}

/**
 * Neumorphic SUNKEN box — matches HTML:
 *   background: #090c12
 *   box-shadow: inset 4px 4px 8px rgba(0,0,0,0.7), inset -3px -3px 6px rgba(255,255,255,0.03)
 *   border: 1px solid rgba(0,0,0,0.6) / rgba(255,255,255,0.05)
 */
fun Modifier.nmSunken(radius: Dp = 12.dp): Modifier = composed {
    val shape = RoundedCornerShape(radius)
    this
        .clip(shape)
        // Sunken dark background — matches HTML #090c12
        .background(Color(0xFF090C12))
        // Asymmetric border: dark top-left (inset shadow), light bottom-right (inset highlight)
        .border(
            width = 1.dp,
            brush = Brush.linearGradient(
                colors = listOf(
                    Color(0x99000000), // top-left: rgba(0,0,0,0.6) — dark inset shadow
                    Color(0x0DFFFFFF), // bottom-right: rgba(255,255,255,0.05) — inset highlight
                ),
                start = Offset.Zero,
                end = Offset.Infinite,
            ),
            shape = shape,
        )
}

@Composable
fun AirTunBackground(content: @Composable () -> Unit) {
    val glass = LocalGlass.current
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    listOf(
                        glass.backgroundGradientTop,
                        glass.backgroundBase,
                        glass.backgroundGradientBottom,
                    )
                )
            )
            // Subtle cyan radial glow — matches HTML: radial-gradient(circle at 15% 20%, rgba(0,229,255,0.04))
            .background(
                Brush.radialGradient(
                    colors = listOf(glass.accent.copy(alpha = 0.04f), Color.Transparent),
                    center = Offset(160f, 280f),  // ~15% x, ~20% y of typical phone screen
                    radius = 1200f,
                )
            ),
    ) {
        content()
    }
}

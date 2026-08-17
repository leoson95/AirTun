package io.airtun.app.ui

import android.widget.Toast
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.Crossfade
import androidx.compose.animation.core.InfiniteRepeatableSpec
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.airtun.app.R
import io.airtun.app.core.ConnectionState
import io.airtun.app.core.ErrorCode
import io.airtun.app.core.WarningCode
import io.airtun.app.service.LocalLog
import io.airtun.app.ui.theme.LocalGlass
import io.airtun.app.ui.theme.glassPanel

@Composable
fun HomeScreen(
    state: ConnectionState,
    batteryExempt: Boolean,
    warnings: Set<WarningCode>,
    themeMode: String,
    logs: List<LocalLog.Entry>,
    onStart: () -> Unit,
    onStop: () -> Unit,
    onRetry: () -> Unit,
    onDismissError: () -> Unit,
    onAllowBattery: () -> Unit,
    onDismissWarning: (WarningCode) -> Unit,
    onSetTheme: (String) -> Unit,
    onClearLogs: () -> Unit,
    onShareLogs: () -> Unit = {},
) {
    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.TopCenter) {
        Column(
            modifier = Modifier
                .widthIn(max = 460.dp)
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .padding(horizontal = 24.dp, vertical = 28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Header()
            Spacer(Modifier.height(20.dp))

            WarningBanners(warnings, onDismissWarning)

            Crossfade(
                targetState = state.stateName,
                animationSpec = tween(280),
                label = "state_transition",
            ) { _ ->
                when (state) {
                    is ConnectionState.Idle -> IdlePanel(onStart)
                    is ConnectionState.Preparing -> PreparingPanel()
                    is ConnectionState.Advertising -> ActivePanel(
                        host = state.host,
                        port = state.port,
                        pinCode = state.pinCode,
                        clientCount = 0,
                        bytesUp = 0,
                        bytesDown = 0,
                        reconnecting = state.reconnecting,
                        onStop = onStop,
                    )
                    is ConnectionState.Connected -> ActivePanel(
                        host = state.host,
                        port = state.port,
                        pinCode = state.pinCode,
                        clientCount = state.clientCount,
                        bytesUp = state.bytesUp,
                        bytesDown = state.bytesDown,
                        reconnecting = state.reconnecting,
                        onStop = onStop,
                    )
                    is ConnectionState.Error -> ErrorPanel(state.code, onRetry, onDismissError)
                }
            }

            Spacer(Modifier.height(20.dp))

            if (!batteryExempt) {
                BatteryBanner(onAllowBattery)
                Spacer(Modifier.height(14.dp))
            }

            AdvancedSection(themeMode, logs, onSetTheme, onClearLogs, onShareLogs)
        }
    }
}

@Composable
private fun Header() {
    val glass = LocalGlass.current
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Column {
            Text(
                text = stringResource(R.string.app_name),
                style = MaterialTheme.typography.displaySmall,
                color = glass.accent,
                fontWeight = FontWeight.ExtraBold,
            )
            Text(
                text = stringResource(R.string.tagline),
                style = MaterialTheme.typography.labelSmall,
                color = glass.textSecondary,
            )
        }
    }
}

@Composable
private fun IdlePanel(onStart: () -> Unit) {
    val glass = LocalGlass.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 28.dp)
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Box(
            modifier = Modifier
                .size(110.dp)
                .clip(CircleShape)
                .background(glass.accentSubtle)
                .clickable(role = Role.Button, onClick = onStart),
            contentAlignment = Alignment.Center,
        ) {
            Box(
                modifier = Modifier
                    .size(80.dp)
                    .clip(CircleShape)
                    .background(glass.accent),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = "▶",
                    color = glass.onAccent,
                    fontSize = 32.sp,
                )
            }
        }

        Spacer(Modifier.height(24.dp))

        Text(
            text = stringResource(R.string.action_start),
            style = MaterialTheme.typography.titleLarge,
            color = glass.textPrimary,
            fontWeight = FontWeight.Bold,
        )

        Spacer(Modifier.height(8.dp))

        Text(
            text = stringResource(R.string.status_idle),
            style = MaterialTheme.typography.bodyMedium,
            color = glass.textSecondary,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun PreparingPanel() {
    val glass = LocalGlass.current
    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val scale by infiniteTransition.animateFloat(
        initialValue = 0.85f,
        targetValue = 1.15f,
        animationSpec = InfiniteRepeatableSpec(
            animation = tween(700),
            repeatMode = RepeatMode.Reverse,
        ),
        label = "scale",
    )

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 28.dp)
            .padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Box(
            modifier = Modifier
                .size(90.dp)
                .scale(scale)
                .clip(CircleShape)
                .background(glass.accentSubtle),
            contentAlignment = Alignment.Center,
        ) {
            Box(
                modifier = Modifier
                    .size(50.dp)
                    .clip(CircleShape)
                    .background(glass.accent),
            )
        }

        Spacer(Modifier.height(24.dp))

        Text(
            text = stringResource(R.string.status_preparing),
            style = MaterialTheme.typography.titleLarge,
            color = glass.textPrimary,
        )
    }
}

@Composable
private fun ActivePanel(
    host: String,
    port: Int,
    pinCode: String,
    clientCount: Int,
    bytesUp: Long,
    bytesDown: Long,
    reconnecting: Boolean,
    onStop: () -> Unit,
) {
    val glass = LocalGlass.current
    val clipboardManager = LocalClipboardManager.current
    val context = LocalContext.current
    var showQr by rememberSaveable { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 28.dp)
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier
                .clip(RoundedCornerShape(12.dp))
                .background(if (clientCount > 0) glass.accentSubtle else glass.fillRaised)
                .padding(horizontal = 14.dp, vertical = 6.dp),
        ) {
            Box(
                modifier = Modifier
                    .size(10.dp)
                    .clip(CircleShape)
                    .background(if (clientCount > 0) glass.accent else glass.warning),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                text = if (reconnecting) {
                    stringResource(R.string.status_reconnecting)
                } else if (clientCount > 0) {
                    pluralStringResource(R.plurals.status_connected, clientCount, clientCount)
                } else {
                    stringResource(R.string.status_waiting)
                },
                style = MaterialTheme.typography.labelSmall,
                color = if (clientCount > 0) glass.accent else glass.textPrimary,
                fontWeight = FontWeight.Bold,
            )
        }

        Spacer(Modifier.height(20.dp))

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .glassPanel(radius = 20.dp, raised = true)
                .clickable {
                    clipboardManager.setText(AnnotatedString(pinCode))
                    Toast.makeText(context, "PIN $pinCode Copied", Toast.LENGTH_SHORT).show()
                }
                .padding(18.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                text = stringResource(R.string.pin_code_label),
                style = MaterialTheme.typography.labelSmall,
                color = glass.textSecondary,
            )
            Spacer(Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                pinCode.forEach { char ->
                    Box(
                        modifier = Modifier
                            .size(48.dp, 56.dp)
                            .clip(RoundedCornerShape(12.dp))
                            .background(glass.fill)
                            .padding(4.dp),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            text = char.toString(),
                            style = MaterialTheme.typography.headlineMedium,
                            color = glass.accent,
                            fontWeight = FontWeight.ExtraBold,
                        )
                    }
                }
            }
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.pin_code_hint),
                style = MaterialTheme.typography.labelSmall,
                color = glass.textTertiary,
                textAlign = TextAlign.Center,
            )
        }

        Spacer(Modifier.height(16.dp))

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .glassPanel(radius = 16.dp, raised = false)
                .padding(horizontal = 16.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column {
                Text(
                    text = stringResource(R.string.socks_endpoint_label),
                    style = MaterialTheme.typography.labelSmall,
                    color = glass.textTertiary,
                )
                Text(
                    text = "$host:$port",
                    style = MaterialTheme.typography.bodyMedium,
                    color = glass.textPrimary,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.SemiBold,
                )
            }
            Column(horizontalAlignment = Alignment.End) {
                Text(
                    text = "Traffic (Up / Down)",
                    style = MaterialTheme.typography.labelSmall,
                    color = glass.textTertiary,
                )
                Text(
                    text = "↑ ${formatBytes(bytesUp)}   ↓ ${formatBytes(bytesDown)}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = glass.accent,
                    fontWeight = FontWeight.Bold,
                )
            }
        }

        Spacer(Modifier.height(12.dp))

        TextButton(onClick = { showQr = !showQr }) {
            Text(
                text = if (showQr) "▲ Hide QR Code" else "▼ ${stringResource(R.string.or_scan_qr)}",
                color = glass.textSecondary,
                style = MaterialTheme.typography.labelSmall,
            )
        }

        AnimatedVisibility(
            visible = showQr,
            enter = expandVertically() + fadeIn(),
            exit = shrinkVertically() + fadeOut(),
        ) {
            Box(
                modifier = Modifier
                    .padding(vertical = 12.dp)
                    .size(200.dp)
                    .glassPanel(radius = 16.dp, raised = true)
                    .padding(16.dp),
                contentAlignment = Alignment.Center,
            ) {
                val qrContent = "airtun://$host:$port?pin=$pinCode"
                QrImage(content = qrContent)
            }
        }

        Spacer(Modifier.height(16.dp))

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(48.dp)
                .clip(RoundedCornerShape(14.dp))
                .background(glass.errorSubtle)
                .clickable(role = Role.Button, onClick = onStop),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = stringResource(R.string.action_stop),
                color = glass.error,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Bold,
            )
        }
    }
}

@Composable
private fun ErrorPanel(
    code: ErrorCode,
    onRetry: () -> Unit,
    onDismiss: () -> Unit,
) {
    val glass = LocalGlass.current
    val (title, body) = when (code) {
        ErrorCode.HOTSPOT_OFF ->
            stringResource(R.string.error_hotspot_off_title) to stringResource(R.string.error_hotspot_off_body)
        ErrorCode.HOTSPOT_LOST ->
            stringResource(R.string.error_hotspot_lost_title) to stringResource(R.string.error_hotspot_lost_body)
        ErrorCode.PORT_IN_USE ->
            stringResource(R.string.error_port_in_use_title) to stringResource(R.string.error_port_in_use_body)
        ErrorCode.SERVICE_FAILED ->
            stringResource(R.string.error_service_failed_title) to stringResource(R.string.error_service_failed_body)
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 28.dp)
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(text = "⚠️", fontSize = 40.sp)
        Spacer(Modifier.height(12.dp))
        Text(
            text = title,
            style = MaterialTheme.typography.titleLarge,
            color = glass.error,
            fontWeight = FontWeight.Bold,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = body,
            style = MaterialTheme.typography.bodyMedium,
            color = glass.textSecondary,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Box(
                modifier = Modifier
                    .weight(1f)
                    .height(44.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .background(glass.fillRaised)
                    .clickable(role = Role.Button, onClick = onDismiss),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = stringResource(R.string.action_dismiss),
                    color = glass.textPrimary,
                    style = MaterialTheme.typography.bodyMedium,
                )
            }
            Box(
                modifier = Modifier
                    .weight(1f)
                    .height(44.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .background(glass.accent)
                    .clickable(role = Role.Button, onClick = onRetry),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = stringResource(R.string.action_retry),
                    color = glass.onAccent,
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Bold,
                )
            }
        }
    }
}

@Composable
private fun WarningBanners(
    warnings: Set<WarningCode>,
    onDismiss: (WarningCode) -> Unit,
) {
    val glass = LocalGlass.current
    warnings.forEach { warning ->
        val (title, body) = when (warning) {
            WarningCode.NO_VPN_ACTIVE ->
                stringResource(R.string.warning_no_vpn_title) to stringResource(R.string.warning_no_vpn_body)
            WarningCode.VPN_CAPTURES_LOCAL ->
                stringResource(R.string.warning_vpn_captures_title) to stringResource(R.string.warning_vpn_captures_body)
        }
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 12.dp)
                .glassPanel(radius = 16.dp)
                .padding(16.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(text = "💡 $title", color = glass.warning, fontWeight = FontWeight.Bold)
                Text(
                    text = "✕",
                    color = glass.textTertiary,
                    modifier = Modifier.clickable { onDismiss(warning) },
                )
            }
            Spacer(Modifier.height(4.dp))
            Text(text = body, color = glass.textSecondary, style = MaterialTheme.typography.labelSmall)
        }
    }
}

@Composable
private fun BatteryBanner(onAllow: () -> Unit) {
    val glass = LocalGlass.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 16.dp)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.battery_banner_title),
                style = MaterialTheme.typography.bodyMedium,
                color = glass.textPrimary,
                fontWeight = FontWeight.Bold,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = stringResource(R.string.battery_banner_body),
                style = MaterialTheme.typography.labelSmall,
                color = glass.textSecondary,
            )
        }
        Spacer(Modifier.width(12.dp))
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(10.dp))
                .background(glass.accent)
                .clickable(role = Role.Button, onClick = onAllow)
                .padding(horizontal = 12.dp, vertical = 8.dp),
        ) {
            Text(
                text = stringResource(R.string.battery_banner_allow),
                color = glass.onAccent,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = FontWeight.Bold,
            )
        }
    }
}

@Composable
private fun AdvancedSection(
    themeMode: String,
    logs: List<LocalLog.Entry>,
    onSetTheme: (String) -> Unit,
    onClearLogs: () -> Unit,
    onShareLogs: () -> Unit,
) {
    val glass = LocalGlass.current
    var expanded by rememberSaveable { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .glassPanel(radius = 20.dp)
            .padding(16.dp),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clickable { expanded = !expanded },
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                text = "⚙️ ${stringResource(R.string.advanced)}",
                style = MaterialTheme.typography.bodyMedium,
                color = glass.textPrimary,
                fontWeight = FontWeight.SemiBold,
            )
            Text(text = if (expanded) "▲" else "▼", color = glass.textTertiary)
        }

        AnimatedVisibility(
            visible = expanded,
            enter = expandVertically() + fadeIn(),
            exit = shrinkVertically() + fadeOut(),
        ) {
            Column(modifier = Modifier.padding(top = 16.dp)) {
                Text(
                    text = stringResource(R.string.advanced_theme),
                    style = MaterialTheme.typography.labelSmall,
                    color = glass.textSecondary,
                )
                Spacer(Modifier.height(8.dp))
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .selectableGroup(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    listOf("system" to R.string.theme_system, "dark" to R.string.theme_dark, "light" to R.string.theme_light).forEach { (mode, resId) ->
                        val selected = themeMode == mode
                        Box(
                            modifier = Modifier
                                .weight(1f)
                                .height(36.dp)
                                .clip(RoundedCornerShape(8.dp))
                                .background(if (selected) glass.accent else glass.fill)
                                .selectable(selected = selected, onClick = { onSetTheme(mode) }),
                            contentAlignment = Alignment.Center,
                        ) {
                            Text(
                                text = stringResource(resId),
                                color = if (selected) glass.onAccent else glass.textSecondary,
                                style = MaterialTheme.typography.labelSmall,
                                fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal,
                            )
                        }
                    }
                }

                Spacer(Modifier.height(16.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        text = stringResource(R.string.advanced_logs),
                        style = MaterialTheme.typography.labelSmall,
                        color = glass.textSecondary,
                    )
                    Row {
                        Text(
                            text = stringResource(R.string.advanced_logs_clear),
                            color = glass.textTertiary,
                            style = MaterialTheme.typography.labelSmall,
                            modifier = Modifier.clickable { onClearLogs() },
                        )
                        Spacer(Modifier.width(12.dp))
                        Text(
                            text = stringResource(R.string.advanced_logs_share),
                            color = glass.accent,
                            style = MaterialTheme.typography.labelSmall,
                            modifier = Modifier.clickable { onShareLogs() },
                        )
                    }
                }

                Spacer(Modifier.height(8.dp))

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 180.dp)
                        .clip(RoundedCornerShape(10.dp))
                        .background(glass.fillRaised)
                        .padding(10.dp)
                        .verticalScroll(rememberScrollState()),
                ) {
                    if (logs.isEmpty()) {
                        Text(
                            text = stringResource(R.string.advanced_logs_empty),
                            color = glass.textTertiary,
                            style = MaterialTheme.typography.labelSmall,
                        )
                    } else {
                        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            logs.forEach { entry ->
                                Text(
                                    text = "${entry.formattedTime}: ${entry.message}",
                                    color = glass.textSecondary,
                                    fontFamily = FontFamily.Monospace,
                                    fontSize = 11.sp,
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

private fun formatBytes(bytes: Long): String {
    return when {
        bytes >= 1_000_000_000 -> "%.1f GB".format(bytes / 1_000_000_000.0)
        bytes >= 1_000_000 -> "%.1f MB".format(bytes / 1_000_000.0)
        bytes >= 1_000 -> "%.1f KB".format(bytes / 1_000.0)
        else -> "$bytes B"
    }
}

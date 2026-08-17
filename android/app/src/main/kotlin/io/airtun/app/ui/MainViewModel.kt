package io.airtun.app.ui

import android.app.Application
import android.content.Context
import android.os.PowerManager
import androidx.lifecycle.AndroidViewModel
import io.airtun.app.core.ConnectionState
import io.airtun.app.core.WarningCode
import io.airtun.app.service.ConnectionRepository
import io.airtun.app.service.LocalLog
import io.airtun.app.service.Settings
import io.airtun.app.service.SharingService
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val settings = Settings(application)

    val state: StateFlow<ConnectionState> = ConnectionRepository.state
    val warnings: StateFlow<Set<WarningCode>> = ConnectionRepository.warnings
    val logs: StateFlow<List<LocalLog.Entry>> = LocalLog.entries

    private val _batteryExempt = MutableStateFlow(readBatteryExempt())
    val batteryExempt: StateFlow<Boolean> = _batteryExempt.asStateFlow()

    private val _themeMode = MutableStateFlow(settings.themeMode)
    val themeMode: StateFlow<String> = _themeMode.asStateFlow()

    fun refreshBatteryExempt() {
        _batteryExempt.value = readBatteryExempt()
    }

    fun startSharing() = SharingService.start(getApplication())

    fun stopSharing() = SharingService.stop(getApplication())

    fun dismissError() {
        ConnectionRepository.dispatch("dismiss") { ConnectionState.Idle }
    }

    fun retry() {
        ConnectionRepository.dispatch("dismiss") { ConnectionState.Idle }
        startSharing()
    }

    fun dismissWarning(code: WarningCode) = ConnectionRepository.setWarning(code, active = false)

    fun setThemeMode(mode: String) {
        settings.themeMode = mode
        _themeMode.value = mode
    }

    fun clearLogs() = LocalLog.clear()

    private fun readBatteryExempt(): Boolean {
        val app = getApplication<Application>()
        val powerManager = app.getSystemService(Context.POWER_SERVICE) as? PowerManager ?: return false
        return powerManager.isIgnoringBatteryOptimizations(app.packageName)
    }
}

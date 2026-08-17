package io.airtun.app.service

import android.content.Context
import android.content.Intent
import android.os.Build
import io.airtun.app.core.ConnectionState
import org.json.JSONArray
import org.json.JSONObject

object DiagnosticReport {

    fun build(state: ConnectionState, logs: List<LocalLog.Entry>, version: String): String {
        val report = JSONObject()
            .put("app", "airtun")
            .put("version", version)
            .put("android_release", Build.VERSION.RELEASE)
            .put("android_sdk", Build.VERSION.SDK_INT)
            .put("device_model", Build.MODEL)
            .put("device_manufacturer", Build.MANUFACTURER)
            .put("state", state.stateName)

        val logArray = JSONArray()
        logs.forEach { entry ->
            logArray.put("${entry.formattedTime}: ${entry.message}")
        }
        report.put("logs", logArray)

        return report.toString(2)
    }

    fun shareIntent(context: Context, reportText: String): Intent {
        return Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"
            putExtra(Intent.EXTRA_SUBJECT, "AirTun Diagnostic Report")
            putExtra(Intent.EXTRA_TEXT, reportText)
        }
    }
}

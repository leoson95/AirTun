package io.airtun.app.net

import android.util.Log
import io.airtun.app.core.AirTunConfig
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import java.io.IOException
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.NetworkInterface

class AirTunBeacon(
    private val deviceName: String,
    private val socksPort: Int = AirTunConfig.DEFAULT_SOCKS_PORT,
    private val pinRequired: Boolean = true,
    private val intervalMs: Long = AirTunConfig.BEACON_INTERVAL_MS,
    private val port: Int = AirTunConfig.DEFAULT_BEACON_PORT,
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var broadcastJob: Job? = null
    private var probeJob: Job? = null

    @Volatile
    var isRunning: Boolean = false
        private set

    fun start() {
        if (isRunning) return
        isRunning = true

        val probeSocket = openProbeSocket()

        broadcastJob = scope.launch {
            DatagramSocket().use { socket ->
                socket.broadcast = true
                val payload = buildBeaconPayload()
                while (isActive) {
                    sendBroadcast(socket, payload)
                    delay(intervalMs)
                }
            }
        }

        probeJob = probeSocket?.let { socket ->
            scope.launch {
                answerProbes(socket)
            }
        }
    }

    fun stop() {
        isRunning = false
        broadcastJob?.cancel()
        broadcastJob = null
        probeJob?.cancel()
        probeJob = null
        scope.cancel()
    }

    private fun openProbeSocket(): DatagramSocket? = try {
        DatagramSocket(null).apply {
            reuseAddress = true
            broadcast = true
            soTimeout = 500
            bind(InetSocketAddress(port))
        }
    } catch (e: IOException) {
        Log.d(TAG, "Could not bind probe listener on port $port: ${e.message}")
        null
    }

    private suspend fun answerProbes(socket: DatagramSocket) {
        socket.use {
            val answer = buildBeaconPayload()
            val buffer = ByteArray(512)
            while (scope.isActive) {
                val packet = DatagramPacket(buffer, buffer.size)
                try {
                    socket.receive(packet)
                } catch (_: IOException) {
                    continue
                }

                val message = String(packet.data, packet.offset, packet.length, Charsets.UTF_8)
                if (isProbe(message)) {
                    try {
                        socket.send(DatagramPacket(answer, answer.size, packet.address, packet.port))
                        Log.d(TAG, "Responded to probe from ${packet.address}:${packet.port}")
                    } catch (e: IOException) {
                        Log.d(TAG, "Failed sending probe answer to ${packet.address}: ${e.message}")
                    }
                }
            }
        }
    }

    fun buildBeaconPayload(): ByteArray {
        val json = buildJsonObject {
            put("app", AirTunConfig.APP_ID)
            put("v", AirTunConfig.PROTOCOL_VERSION)
            put("device", deviceName.take(64))
            put("port", socksPort)
            put("pin_required", pinRequired)
        }
        return json.toString().toByteArray(Charsets.UTF_8)
    }

    private fun sendBroadcast(socket: DatagramSocket, bytes: ByteArray) {
        val targets = broadcastAddresses()
        for (address in targets) {
            try {
                socket.send(DatagramPacket(bytes, bytes.size, address, port))
            } catch (e: IOException) {
            }
        }
        try {
            val global = InetAddress.getByName("255.255.255.255")
            socket.send(DatagramPacket(bytes, bytes.size, global, port))
        } catch (_: Exception) {}
    }

    private fun broadcastAddresses(): List<InetAddress> = try {
        NetworkInterface.getNetworkInterfaces().toList()
            .filter { it.isUp && !it.isLoopback }
            .flatMap { it.interfaceAddresses }
            .mapNotNull { it.broadcast }
    } catch (_: Exception) {
        emptyList()
    }

    companion object {
        private const val TAG = "AirTunBeacon"

        fun isProbe(text: String): Boolean = try {
            val obj = Json.parseToJsonElement(text).jsonObject
            val app = obj["app"]?.jsonPrimitive?.content
            val isAirtun = app == null || app.isEmpty() || app == AirTunConfig.APP_ID
            val probe = obj["probe"]?.jsonPrimitive
            val isProbeValid = probe != null && (probe.booleanOrNull == true || (probe.intOrNull ?: 0) != 0 || probe.content == "1")
            isAirtun && isProbeValid
        } catch (_: Exception) {
            false
        }
    }
}

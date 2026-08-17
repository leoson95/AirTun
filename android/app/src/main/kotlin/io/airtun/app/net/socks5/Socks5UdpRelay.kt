package io.airtun.app.net.socks5

import android.util.Log
import io.airtun.app.core.AirTunConfig
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.IOException
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.Inet4Address
import java.net.Inet6Address
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.SocketAddress
import java.nio.ByteBuffer
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

class Socks5UdpRelay(
    private val onTraffic: (bytesUp: Long, bytesDown: Long) -> Unit,
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var relaySocket: DatagramSocket? = null
    private var relayJob: Job? = null

    var boundPort: Int = -1
        private set

    private val activeClients = ConcurrentHashMap<SocketAddress, Long>()

    fun start(): Int {
        val socket = DatagramSocket(0)
        relaySocket = socket
        boundPort = socket.localPort

        relayJob = scope.launch {
            val buffer = ByteArray(AirTunConfig.BUFFER_SIZE)
            while (isActive) {
                val packet = DatagramPacket(buffer, buffer.size)
                try {
                    socket.receive(packet)
                } catch (_: IOException) {
                    break
                }

                val senderAddress = packet.socketAddress
                activeClients[senderAddress] = System.currentTimeMillis()

                val dataLength = packet.length
                if (dataLength < 10) continue

                val byteBuffer = ByteBuffer.wrap(packet.data, packet.offset, dataLength)
                val rsv = byteBuffer.short
                val frag = byteBuffer.get()
                if (frag.toInt() != 0) {
                    continue
                }

                val atyp = byteBuffer.get().toInt() and 0xFF
                val targetAddress: InetAddress?
                val targetPort: Int

                when (atyp) {
                    0x01 -> {
                        val ipBytes = ByteArray(4)
                        byteBuffer.get(ipBytes)
                        targetAddress = InetAddress.getByAddress(ipBytes)
                        targetPort = byteBuffer.short.toInt() and 0xFFFF
                    }
                    0x03 -> {
                        val len = byteBuffer.get().toInt() and 0xFF
                        val domainBytes = ByteArray(len)
                        byteBuffer.get(domainBytes)
                        val domain = String(domainBytes, Charsets.US_ASCII)
                        targetAddress = try {
                            InetAddress.getByName(domain)
                        } catch (_: Exception) {
                            null
                        }
                        targetPort = byteBuffer.short.toInt() and 0xFFFF
                    }
                    0x04 -> {
                        val ipBytes = ByteArray(16)
                        byteBuffer.get(ipBytes)
                        targetAddress = InetAddress.getByAddress(ipBytes)
                        targetPort = byteBuffer.short.toInt() and 0xFFFF
                    }
                    else -> continue
                }

                if (targetAddress == null) continue

                val payloadLength = byteBuffer.remaining()
                val payload = ByteArray(payloadLength)
                byteBuffer.get(payload)

                onTraffic(payloadLength.toLong(), 0L)

                scope.launch {
                    forwardAndListen(
                        targetAddress = targetAddress,
                        targetPort = targetPort,
                        payload = payload,
                        clientEndpoint = senderAddress,
                    )
                }
            }
        }
        return boundPort
    }

    private fun forwardAndListen(
        targetAddress: InetAddress,
        targetPort: Int,
        payload: ByteArray,
        clientEndpoint: SocketAddress,
    ) {
        try {
            DatagramSocket().use { remoteSocket ->
                remoteSocket.soTimeout = 10000
                val outgoingPacket = DatagramPacket(payload, payload.size, targetAddress, targetPort)
                remoteSocket.send(outgoingPacket)

                val responseBuffer = ByteArray(AirTunConfig.BUFFER_SIZE)
                val incomingPacket = DatagramPacket(responseBuffer, responseBuffer.size)
                remoteSocket.receive(incomingPacket)

                val respLength = incomingPacket.length
                onTraffic(0L, respLength.toLong())

                val respAddress = incomingPacket.address
                val respPort = incomingPacket.port

                val headerBuffer = ByteBuffer.allocate(32)
                headerBuffer.putShort(0.toShort())
                headerBuffer.put(0.toByte())
                if (respAddress is Inet4Address) {
                    headerBuffer.put(0x01.toByte())
                    headerBuffer.put(respAddress.address)
                } else if (respAddress is Inet6Address) {
                    headerBuffer.put(0x04.toByte())
                    headerBuffer.put(respAddress.address)
                }
                headerBuffer.putShort(respPort.toShort())

                val headerBytes = ByteArray(headerBuffer.position())
                headerBuffer.flip()
                headerBuffer.get(headerBytes)

                val fullResponse = ByteArray(headerBytes.size + respLength)
                System.arraycopy(headerBytes, 0, fullResponse, 0, headerBytes.size)
                System.arraycopy(incomingPacket.data, incomingPacket.offset, fullResponse, headerBytes.size, respLength)

                relaySocket?.send(DatagramPacket(fullResponse, fullResponse.size, clientEndpoint))
            }
        } catch (_: Exception) {
        }
    }

    fun stop() {
        relayJob?.cancel()
        relayJob = null
        try {
            relaySocket?.close()
        } catch (_: Exception) {}
        relaySocket = null
        scope.cancel()
    }
}

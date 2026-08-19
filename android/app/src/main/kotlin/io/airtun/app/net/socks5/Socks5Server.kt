package io.airtun.app.net.socks5

import android.util.Log
import io.airtun.app.core.AirTunConfig
import io.airtun.app.net.LocalAddress
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.IOException
import java.net.Inet4Address
import java.net.Inet6Address
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.net.SocketTimeoutException
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong

class Socks5Server(
    val port: Int = AirTunConfig.DEFAULT_SOCKS_PORT,
    var pinCode: String = "",
    var pinRequired: Boolean = true,
    private val onTraffic: (bytesUp: Long, bytesDown: Long) -> Unit,
    private val onClientCountChanged: (count: Int) -> Unit,
    private val onLog: (message: String) -> Unit = {},
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var serverSocket: ServerSocket? = null
    private var acceptJob: Job? = null
    private var udpRelay: Socks5UdpRelay? = null

    val activeConnections = AtomicInteger(0)
    private val activeClients = ConcurrentHashMap<String, AtomicInteger>()
    val totalBytesUp = AtomicLong(0)
    val totalBytesDown = AtomicLong(0)

    private val authenticatedClients = ConcurrentHashMap.newKeySet<String>()

    @Volatile
    var isRunning: Boolean = false
        private set

    @Synchronized
    fun start() {
        if (isRunning) return
        val server = ServerSocket()
        server.reuseAddress = true
        server.bind(InetSocketAddress(port))
        serverSocket = server
        isRunning = true

        udpRelay = Socks5UdpRelay { up, down ->
            recordTraffic(up, down)
        }.also { it.start() }

        onLog("SOCKS5 Server listening on port $port (UDP Relay on ${udpRelay?.boundPort})")

        acceptJob = scope.launch {
            while (isActive) {
                val clientSocket = try {
                    server.accept()
                } catch (_: IOException) {
                    break
                }
                launch {
                    handleClient(clientSocket)
                }
            }
        }
    }

    private suspend fun handleClient(client: Socket) {
        val clientIp = client.inetAddress?.hostAddress ?: "unknown"
        activeConnections.incrementAndGet()
        activeClients.computeIfAbsent(clientIp) { AtomicInteger(0) }.incrementAndGet()
        onClientCountChanged(activeClients.size)

        try {
            client.soTimeout = AirTunConfig.SOCKET_IDLE_TIMEOUT_MS
            client.tcpNoDelay = true

            val clientIn = DataInputStream(BufferedInputStream(client.getInputStream()))
            val clientOut = DataOutputStream(BufferedOutputStream(client.getOutputStream()))

            val version = clientIn.readUnsignedByte()
            if (version != 0x05) {
                return
            }

            val nMethods = clientIn.readUnsignedByte()
            val methods = ByteArray(nMethods)
            clientIn.readFully(methods)

            val isAlreadyAuth = !pinRequired || authenticatedClients.contains(clientIp)
            val hasUserPass = methods.contains(0x02.toByte())
            val hasNoAuth = methods.contains(0x00.toByte())

            if (isAlreadyAuth && hasNoAuth) {
                clientOut.writeByte(0x05)
                clientOut.writeByte(0x00)
                clientOut.flush()
            } else if (pinRequired && hasUserPass) {
                clientOut.writeByte(0x05)
                clientOut.writeByte(0x02)
                clientOut.flush()

                val authVer = clientIn.readUnsignedByte()
                if (authVer != 0x01) {
                    clientOut.writeByte(0x01)
                    clientOut.writeByte(0xFF)
                    clientOut.flush()
                    return
                }

                val ulen = clientIn.readUnsignedByte()
                val unameBytes = ByteArray(ulen)
                clientIn.readFully(unameBytes)
                val uname = String(unameBytes, Charsets.UTF_8)

                val plen = clientIn.readUnsignedByte()
                val passBytes = ByteArray(plen)
                clientIn.readFully(passBytes)
                val pass = String(passBytes, Charsets.UTF_8)

                val matchesPin = (uname == pinCode || pass == pinCode)
                if (matchesPin) {
                    authenticatedClients.add(clientIp)
                    clientOut.writeByte(0x01)
                    clientOut.writeByte(0x00)
                    clientOut.flush()
                    onLog("Client $clientIp authenticated successfully with PIN")
                } else {
                    clientOut.writeByte(0x01)
                    clientOut.writeByte(0xFF)
                    clientOut.flush()
                    onLog("Client $clientIp failed PIN authentication")
                    return
                }
            } else if (isAlreadyAuth) {
                clientOut.writeByte(0x05)
                clientOut.writeByte(0x00)
                clientOut.flush()
            } else {
                clientOut.writeByte(0x05)
                clientOut.writeByte(0xFF)
                clientOut.flush()
                return
            }

            val reqVer = clientIn.readUnsignedByte()
            if (reqVer != 0x05) return

            val cmd = clientIn.readUnsignedByte()
            val rsv = clientIn.readUnsignedByte()
            val atyp = clientIn.readUnsignedByte()

            val targetHost: String
            val targetAddress: InetAddress?

            when (atyp) {
                0x01 -> {
                    val ipBytes = ByteArray(4)
                    clientIn.readFully(ipBytes)
                    targetAddress = InetAddress.getByAddress(ipBytes)
                    targetHost = targetAddress.hostAddress ?: ""
                }
                0x03 -> {
                    val len = clientIn.readUnsignedByte()
                    val domainBytes = ByteArray(len)
                    clientIn.readFully(domainBytes)
                    targetHost = String(domainBytes, Charsets.US_ASCII)
                    targetAddress = try {
                        InetAddress.getByName(targetHost)
                    } catch (_: Exception) {
                        null
                    }
                }
                0x04 -> {
                    val ipBytes = ByteArray(16)
                    clientIn.readFully(ipBytes)
                    targetAddress = InetAddress.getByAddress(ipBytes)
                    targetHost = targetAddress.hostAddress ?: ""
                }
                else -> {
                    sendReply(clientOut, 0x08)
                    return
                }
            }

            val targetPort = clientIn.readUnsignedShort()

            when (cmd) {
                0x01 -> {
                    if (targetAddress == null) {
                        sendReply(clientOut, 0x04)
                        return
                    }

                    val remoteSocket = try {
                        Socket().apply {
                            soTimeout = AirTunConfig.SOCKET_IDLE_TIMEOUT_MS
                            tcpNoDelay = true
                            connect(InetSocketAddress(targetAddress, targetPort), 10000)
                        }
                    } catch (_: Exception) {
                        sendReply(clientOut, 0x05)
                        return
                    }

                    sendReply(clientOut, 0x00, remoteSocket.localAddress, remoteSocket.localPort)
                    pipeSockets(client, remoteSocket)
                }

                0x03 -> {
                    val relay = udpRelay
                    if (relay == null || relay.boundPort <= 0) {
                        sendReply(clientOut, 0x01)
                        return
                    }
                    val bindAddr = (client.localAddress as? Inet4Address)
                        ?: LocalAddress.findAdvertisableIpv4()?.let { try { InetAddress.getByName(it) } catch (_: Exception) { null } }
                        ?: InetAddress.getByName("0.0.0.0")
                    sendReply(clientOut, 0x00, bindAddr, relay.boundPort)

                    try {
                        val dummy = ByteArray(64)
                        while (clientIn.read(dummy) != -1) {
                        }
                    } catch (_: Exception) {}
                }

                else -> {
                    sendReply(clientOut, 0x07)
                }
            }

        } catch (_: SocketTimeoutException) {
        } catch (_: IOException) {
        } finally {
            try { client.close() } catch (_: Exception) {}
            activeConnections.decrementAndGet().coerceAtLeast(0)
            activeClients.computeIfPresent(clientIp) { _, ref ->
                if (ref.decrementAndGet() <= 0) null else ref
            }
            onClientCountChanged(activeClients.size)
        }
    }

    private fun sendReply(
        out: DataOutputStream,
        repCode: Int,
        bndAddr: InetAddress = InetAddress.getByName("0.0.0.0"),
        bndPort: Int = 0,
    ) {
        try {
            out.writeByte(0x05)
            out.writeByte(repCode)
            out.writeByte(0x00)
            if (bndAddr is Inet6Address) {
                out.writeByte(0x04)
                out.write(bndAddr.address)
            } else {
                out.writeByte(0x01)
                out.write(bndAddr.address)
            }
            out.writeShort(bndPort)
            out.flush()
        } catch (_: Exception) {}
    }

    private suspend fun pipeSockets(client: Socket, remote: Socket) {
        val clientIn = client.getInputStream()
        val clientOut = client.getOutputStream()
        val remoteIn = remote.getInputStream()
        val remoteOut = remote.getOutputStream()

        val uploadJob = scope.launch {
            val buf = ByteArray(AirTunConfig.BUFFER_SIZE)
            try {
                while (isActive) {
                    val read = clientIn.read(buf)
                    if (read == -1) break
                    remoteOut.write(buf, 0, read)
                    remoteOut.flush()
                    recordTraffic(read.toLong(), 0L)
                }
            } catch (_: Exception) {} finally {
                try { remote.shutdownOutput() } catch (_: Exception) {}
            }
        }

        val downloadJob = scope.launch {
            val buf = ByteArray(AirTunConfig.BUFFER_SIZE)
            try {
                while (isActive) {
                    val read = remoteIn.read(buf)
                    if (read == -1) break
                    clientOut.write(buf, 0, read)
                    clientOut.flush()
                    recordTraffic(0L, read.toLong())
                }
            } catch (_: Exception) {} finally {
                try { client.shutdownOutput() } catch (_: Exception) {}
            }
        }

        try {
            uploadJob.join()
            downloadJob.join()
        } finally {
            try { remote.close() } catch (_: Exception) {}
        }
    }

    private fun recordTraffic(up: Long, down: Long) {
        if (up > 0) totalBytesUp.addAndGet(up)
        if (down > 0) totalBytesDown.addAndGet(down)
        onTraffic(up, down)
    }

    @Synchronized
    fun stop() {
        isRunning = false
        acceptJob?.cancel()
        acceptJob = null
        try {
            serverSocket?.close()
        } catch (_: Exception) {}
        serverSocket = null
        udpRelay?.stop()
        udpRelay = null
        authenticatedClients.clear()
        activeConnections.set(0)
        onClientCountChanged(0)
        scope.cancel()
    }
}

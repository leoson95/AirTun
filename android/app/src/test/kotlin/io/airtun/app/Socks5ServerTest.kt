package io.airtun.app

import io.airtun.app.net.socks5.Socks5Server
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.DataInputStream
import java.io.DataOutputStream
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.atomic.AtomicInteger

class Socks5ServerTest {

    private var server: Socks5Server? = null
    private val testPort = 19876
    private var echoServer: ServerSocket? = null

    @Before
    fun setUp() {
        server = Socks5Server(
            port = testPort,
            pinCode = "5678",
            pinRequired = true,
            onTraffic = { _, _ -> },
            onClientCountChanged = {},
        ).also { it.start() }
    }

    @After
    fun tearDown() {
        server?.stop()
        echoServer?.close()
    }

    @Test
    fun serverRejectsInvalidPin() {
        Socket().use { client ->
            client.connect(InetSocketAddress("127.0.0.1", testPort), 2000)
            val out = DataOutputStream(client.getOutputStream())
            val `in` = DataInputStream(client.getInputStream())

            out.writeByte(0x05)
            out.writeByte(0x01)
            out.writeByte(0x02)
            out.flush()

            val ver = `in`.readUnsignedByte()
            val method = `in`.readUnsignedByte()
            assertEquals(0x05, ver)
            assertEquals(0x02, method)

            out.writeByte(0x01)
            out.writeByte(4)
            out.write("0000".toByteArray(Charsets.UTF_8))
            out.writeByte(4)
            out.write("0000".toByteArray(Charsets.UTF_8))
            out.flush()

            val authVer = `in`.readUnsignedByte()
            val authStatus = `in`.readUnsignedByte()
            assertEquals(0x01, authVer)
            assertEquals(0xFF, authStatus)
        }
    }

    @Test
    fun serverAcceptsValidPin() {
        Socket().use { client ->
            client.connect(InetSocketAddress("127.0.0.1", testPort), 2000)
            val out = DataOutputStream(client.getOutputStream())
            val `in` = DataInputStream(client.getInputStream())

            out.writeByte(0x05)
            out.writeByte(0x01)
            out.writeByte(0x02)
            out.flush()

            assertEquals(0x05, `in`.readUnsignedByte())
            assertEquals(0x02, `in`.readUnsignedByte())

            out.writeByte(0x01)
            out.writeByte(4)
            out.write("5678".toByteArray(Charsets.UTF_8))
            out.writeByte(4)
            out.write("5678".toByteArray(Charsets.UTF_8))
            out.flush()

            val authVer = `in`.readUnsignedByte()
            val authStatus = `in`.readUnsignedByte()
            assertEquals(0x01, authVer)
            assertEquals(0x00, authStatus)
        }
    }
}

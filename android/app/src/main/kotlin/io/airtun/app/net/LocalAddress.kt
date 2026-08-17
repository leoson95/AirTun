package io.airtun.app.net

import java.net.Inet4Address
import java.net.NetworkInterface

object LocalAddress {

    data class Candidate(val interfaceName: String, val ip: String)

    fun findAdvertisableIpv4(): String? = choose(enumerate())

    private fun enumerate(): List<Candidate> {
        val interfaces = try {
            NetworkInterface.getNetworkInterfaces()?.toList() ?: return emptyList()
        } catch (_: Exception) {
            return emptyList()
        }
        return interfaces
            .filter { runCatching { it.isUp && !it.isLoopback }.getOrDefault(false) }
            .flatMap { nic ->
                nic.inetAddresses.toList()
                    .filterIsInstance<Inet4Address>()
                    .filter { it.isSiteLocalAddress }
                    .map { Candidate(nic.name.lowercase(), it.hostAddress ?: "") }
            }
            .filter { it.ip.isNotEmpty() && isReachableFromClient(it.interfaceName) }
    }

    internal fun isReachableFromClient(interfaceName: String): Boolean =
        UNREACHABLE_HINTS.none { interfaceName.contains(it) }

    internal fun choose(candidates: List<Candidate>): String? =
        candidates.maxByOrNull { score(it.interfaceName, it.ip) }?.ip

    internal fun score(interfaceName: String, ip: String): Int {
        var score = 1
        when {
            AP_HINTS.any { interfaceName.startsWith(it) } -> score += 3
            interfaceName.startsWith("wlan") -> score += 2
            interfaceName.startsWith("eth") || interfaceName.startsWith("en") -> score += 1
        }
        if (ip.endsWith(".1") && AP_HINTS.any { interfaceName.startsWith(it) }) score += 1
        return score
    }

    private val AP_HINTS = listOf("ap", "swlan", "softap", "wlan1", "wigig")

    private val UNREACHABLE_HINTS = listOf(
        "rmnet", "ccmni", "pdp", "seth", "wwan", "qmi", "ppp",
        "tun", "ipsec", "dummy",
    )
}

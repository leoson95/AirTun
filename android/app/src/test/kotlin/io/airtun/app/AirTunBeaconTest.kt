package io.airtun.app

import io.airtun.app.net.AirTunBeacon
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AirTunBeaconTest {

    @Test
    fun buildBeaconPayloadContainsCorrectFields() {
        val beacon = AirTunBeacon(deviceName = "Pixel 8 Pro", socksPort = 10808, pinRequired = true)
        val payloadBytes = beacon.buildBeaconPayload()
        val text = String(payloadBytes, Charsets.UTF_8)

        assertTrue(text.contains("\"app\":\"airtun\""))
        assertTrue(text.contains("\"v\":1"))
        assertTrue(text.contains("\"device\":\"Pixel 8 Pro\""))
        assertTrue(text.contains("\"port\":10808"))
        assertTrue(text.contains("\"pin_required\":true"))
    }

    @Test
    fun isProbeIdentifiesValidProbes() {
        assertTrue(AirTunBeacon.isProbe("""{"app":"airtun","probe":1}"""))
        assertTrue(AirTunBeacon.isProbe("""{"probe":true}"""))
        assertTrue(AirTunBeacon.isProbe("""{"app":"airtun","probe":"1"}"""))
        assertFalse(AirTunBeacon.isProbe("""{"app":"otherapp","probe":1}"""))
        assertFalse(AirTunBeacon.isProbe("""{"app":"airtun","probe":0}"""))
        assertFalse(AirTunBeacon.isProbe("""not json"""))
    }
}

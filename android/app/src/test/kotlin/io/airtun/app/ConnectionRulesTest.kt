package io.airtun.app

import io.airtun.app.core.ConnectionRules
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectionRulesTest {

    @Test
    fun idleCanStart() {
        assertTrue(ConnectionRules.canTransition("Idle", "start"))
        assertEquals("Preparing", ConnectionRules.target("Idle", "start"))
    }

    @Test
    fun preparingTransitions() {
        assertTrue(ConnectionRules.canTransition("Preparing", "ready"))
        assertEquals("Advertising", ConnectionRules.target("Preparing", "ready"))

        assertTrue(ConnectionRules.canTransition("Preparing", "failure"))
        assertEquals("Error", ConnectionRules.target("Preparing", "failure"))

        assertTrue(ConnectionRules.canTransition("Preparing", "stop"))
        assertEquals("Idle", ConnectionRules.target("Preparing", "stop"))
    }

    @Test
    fun advertisingTransitions() {
        assertTrue(ConnectionRules.canTransition("Advertising", "clientConnected"))
        assertEquals("Connected", ConnectionRules.target("Advertising", "clientConnected"))

        assertTrue(ConnectionRules.canTransition("Advertising", "stop"))
        assertEquals("Idle", ConnectionRules.target("Advertising", "stop"))
    }

    @Test
    fun connectedTransitions() {
        assertTrue(ConnectionRules.canTransition("Connected", "lastClientDisconnected"))
        assertEquals("Advertising", ConnectionRules.target("Connected", "lastClientDisconnected"))

        assertTrue(ConnectionRules.canTransition("Connected", "stop"))
        assertEquals("Idle", ConnectionRules.target("Connected", "stop"))
    }

    @Test
    fun invalidTransitionsRejected() {
        assertFalse(ConnectionRules.canTransition("Idle", "ready"))
        assertFalse(ConnectionRules.canTransition("Advertising", "start"))
        assertFalse(ConnectionRules.canTransition("Connected", "ready"))
    }
}

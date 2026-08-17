package io.airtun.app

import io.airtun.app.core.PinCode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PinCodeTest {

    @Test
    fun drawGeneratesValidFourDigitPin() {
        for (i in 0 until 50) {
            val pin = PinCode.draw()
            assertEquals(4, pin.length)
            assertTrue(pin.all { it in '0'..'9' })
            assertTrue(PinCode.isValid(pin))
        }
    }

    @Test
    fun isValidRejectsBadFormats() {
        assertFalse(PinCode.isValid(null))
        assertFalse(PinCode.isValid(""))
        assertFalse(PinCode.isValid("123"))
        assertFalse(PinCode.isValid("12345"))
        assertFalse(PinCode.isValid("12a4"))
        assertFalse(PinCode.isValid("----"))
    }

    @Test
    fun normalizeStripsWhitespace() {
        assertEquals("1234", PinCode.normalize(" 12 34 "))
        assertEquals(null, PinCode.normalize("123"))
        assertEquals(null, PinCode.normalize("abcd"))
    }
}

package io.airtun.app.core

import java.security.SecureRandom

/**
 * 4-digit numeric PIN for AirTun connection authentication and discovery pairing.
 * Generates a clean 4-digit numeric string (1000..9999) with no ambiguous chars.
 */
object PinCode {

    const val LENGTH = 4
    const val MIN = 1000
    const val MAX = 9999

    private val random = SecureRandom()

    fun draw(): String {
        return (MIN + random.nextInt(MAX - MIN + 1)).toString()
    }

    fun isValid(input: String?): Boolean {
        val normalized = normalize(input) ?: return false
        return normalized.length == LENGTH
    }

    fun normalize(input: String?): String? {
        if (input == null) return null
        val trimmed = input.filterNot { it.isWhitespace() }
        if (trimmed.length != LENGTH) return null
        if (!trimmed.all { it in '0'..'9' }) return null
        return trimmed
    }
}

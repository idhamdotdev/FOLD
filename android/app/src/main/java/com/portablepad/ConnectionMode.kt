package com.FOLD

/**
 * Describes how the app connects to the PC.
 *
 *  WiFi  → host = user-entered IP (e.g. "192.168.1.10")
 *  Usb   → host = "127.0.0.1"  (ADB reverse tunnel; PC set up adb reverse)
 */
sealed class ConnectionMode(val host: String) {
    class WiFi(ip: String) : ConnectionMode(ip)
    object Usb : ConnectionMode("127.0.0.1")
}

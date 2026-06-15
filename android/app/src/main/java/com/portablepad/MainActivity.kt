package com.FOLD

import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.FOLD.databinding.ActivityMainBinding
import kotlinx.coroutines.*

class MainActivity : AppCompatActivity() {

    private lateinit var b: ActivityMainBinding
    private val scope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        b = ActivityMainBinding.inflate(layoutInflater)
        setContentView(b.root)

        // Restore last saved IP
        b.etIpAddress.setText(PrefsManager.getLastIp(this))

        // ── Mode toggle ──────────────────────────────────────────────────────
        setMode(PrefsManager.getLastMode(this))

        b.rbUsb.setOnClickListener { setMode(MODE_USB) }
        b.rbWifi.setOnClickListener { setMode(MODE_WIFI) }

        // ── Connect button ──────────────────────────────────────────────────
        b.btnConnect.setOnClickListener {
            if (b.rbUsb.isChecked) {
                connectUsb()
            } else {
                connectWifi()
            }
        }
    }

    // ── Wi-Fi connection ─────────────────────────────────────────────────────

    private fun connectWifi() {
        val ip = b.etIpAddress.text.toString().trim()
        if (ip.isEmpty()) { toast("Enter PC IP address"); return }
        PrefsManager.saveLastIp(this, ip)
        launchConnection(ConnectionMode.WiFi(ip))
    }

    // ── USB connection ────────────────────────────────────────────────────────

    private fun connectUsb() {
        launchConnection(ConnectionMode.Usb)
    }

    // ── Shared launch logic ───────────────────────────────────────────────────

    private fun launchConnection(mode: ConnectionMode) {
        b.btnConnect.isEnabled  = false
        b.tvStatus.text = "Connecting..."
        b.tvStatus.visibility = View.VISIBLE

        scope.launch {
            val ok = withContext(Dispatchers.IO) { testConnection(mode.host) }
            if (ok) {
                b.tvStatus.visibility = View.GONE
                PrefsManager.saveLastMode(this@MainActivity,
                    if (mode is ConnectionMode.Usb) MODE_USB else MODE_WIFI)
                startActivity(
                    Intent(this@MainActivity, DisplayActivity::class.java)
                        .putExtra("host", mode.host)
                        .putExtra("mode", if (mode is ConnectionMode.Usb) MODE_USB else MODE_WIFI)
                )
            } else {
                b.tvStatus.text = "Can't Connect, Try Again...."
                b.tvStatus.visibility = View.VISIBLE
            }
            b.btnConnect.isEnabled = true
        }
    }

    // ── Mode UI helper ────────────────────────────────────────────────────────

    private fun setMode(mode: String) {
        val isUsb = mode == MODE_USB
        b.rbUsb.isChecked = isUsb
        b.rbWifi.isChecked = !isUsb
        b.etIpAddress.isEnabled = !isUsb
        b.tvStatus.visibility = View.GONE
    }

    private fun testConnection(host: String): Boolean = try {
        java.net.Socket().use { socket ->
            socket.connect(java.net.InetSocketAddress(host, 8765), 2000)
            true
        }
    } catch (e: Exception) { false }

    private fun toast(msg: String) = Toast.makeText(this, msg, Toast.LENGTH_SHORT).show()

    override fun onDestroy() { super.onDestroy(); scope.cancel() }

    companion object {
        const val MODE_WIFI = "wifi"
        const val MODE_USB  = "usb"
    }
}

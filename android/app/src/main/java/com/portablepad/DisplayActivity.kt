package com.FOLD

import android.os.Bundle
import android.view.*
import androidx.appcompat.app.AppCompatActivity
import com.FOLD.databinding.ActivityDisplayBinding
import kotlinx.coroutines.*

class DisplayActivity : AppCompatActivity() {

    private lateinit var b: ActivityDisplayBinding
    private lateinit var touchSender: TouchSender
    private var screenW = 1280
    private var screenH = 720

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        b = ActivityDisplayBinding.inflate(layoutInflater)
        setContentView(b.root)

        window.addFlags(
            WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON or
            WindowManager.LayoutParams.FLAG_FULLSCREEN or
            WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS
        )
        window.decorView.systemUiVisibility = (
            View.SYSTEM_UI_FLAG_FULLSCREEN or
            View.SYSTEM_UI_FLAG_HIDE_NAVIGATION or
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY or
            View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN or
            View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
        )

        // host = Wi-Fi IP  OR  "127.0.0.1" (USB mode)
        val host = intent.getStringExtra("host") ?: "127.0.0.1"
        touchSender = TouchSender(host, 8766)

        // Get tablet's exact physical display boundaries and refresh rate
        val metrics = android.util.DisplayMetrics()
        val display = if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
            display
        } else {
            @Suppress("DEPRECATION")
            windowManager.defaultDisplay
        }
        @Suppress("DEPRECATION")
        display?.getRealMetrics(metrics)
        screenW = metrics.widthPixels
        screenH = metrics.heightPixels
        val refreshRate = (display?.refreshRate ?: 60f).toInt()

        // Force Android window to use the tablet's maximum supported hardware refresh rate (e.g., 120Hz)
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.M) {
            val modes = display?.supportedModes
            if (modes != null) {
                val bestMode = modes.maxByOrNull { it.refreshRate }
                if (bestMode != null && bestMode.refreshRate > 60f) {
                    val params = window.attributes
                    params.preferredDisplayModeId = bestMode.modeId
                    window.attributes = params
                }
            }
        }

        b.h264View.setOnTouchListener { v, event ->
            val normX = event.x / v.width
            val normY = event.y / v.height
            val type  = when (event.action) {
                MotionEvent.ACTION_DOWN -> "down"
                MotionEvent.ACTION_MOVE -> "move"
                MotionEvent.ACTION_UP   -> "up"
                else -> return@setOnTouchListener false
            }
            touchSender.send(type, normX, normY, event.getPointerId(event.actionIndex))
            true
        }

        // Route status messages to the TextView overlay — never via lockCanvas.
        // When msg is empty the overlay hides itself so video is unobstructed.
        b.h264View.statusListener = { msg ->
            if (msg.isEmpty()) {
                b.statusOverlay.visibility = View.GONE
            } else {
                b.statusText.text = msg
                b.statusOverlay.visibility = View.VISIBLE
            }
        }

        // Route real-time stream stats to the top-left overlay
        b.h264View.statsListener = { stats ->
            if (stats.isEmpty()) {
                b.tvStats.visibility = View.GONE
            } else {
                b.tvStats.text = stats
                b.tvStats.visibility = View.VISIBLE
            }
        }

        // Setup listener for the disconnect button overlay
        b.btnDisconnect.setOnClickListener {
            showExitConfirmationDialog()
        }

        // Handshake screen details to the Windows server for auto-resolution matching
        touchSender.sendConfig(screenW, screenH, refreshRate)

        b.h264View.start(host, screenW, screenH)
    }

    override fun onBackPressed() {
        showExitConfirmationDialog()
    }

    private fun showExitConfirmationDialog() {
        val dialog = android.app.Dialog(this)
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE)

        val dialogBinding = com.FOLD.databinding.DialogConfirmExitBinding.inflate(layoutInflater)
        dialog.setContentView(dialogBinding.root)

        // Set background transparent so our custom border is visible
        dialog.window?.setBackgroundDrawable(android.graphics.drawable.ColorDrawable(android.graphics.Color.TRANSPARENT))
        dialog.setCancelable(false)

        dialogBinding.btnYes.setOnClickListener {
            dialog.dismiss()
            finish()
        }

        dialogBinding.btnNo.setOnClickListener {
            dialog.dismiss()
        }

        dialog.show()
    }

    override fun onStop()    { super.onStop(); b.h264View.stop() }
    override fun onRestart() {
        super.onRestart()
        val host = intent.getStringExtra("host") ?: "127.0.0.1"
        b.h264View.start(host, screenW, screenH)
    }

    override fun onDestroy() {
        super.onDestroy()
        touchSender.destroy()
    }
}

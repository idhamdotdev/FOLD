package com.FOLD

import kotlinx.coroutines.*
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody

/**
 * Sends touch events to the Windows PC via HTTP POST on a background coroutine.
 * Fire-and-forget: failed requests are silently dropped to avoid input lag.
 *
 * Thread-safe. Call [destroy] when the activity is destroyed.
 */
class TouchSender(private val host: String, private val port: Int) {

    private val client = OkHttpClient()
    private val scope  = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val json   = "application/json; charset=utf-8".toMediaType()
    private val url    = "http://$host:$port/"

    /**
     * @param type      "down" | "move" | "up"
     * @param normX     0.0–1.0 horizontal position on PC screen
     * @param normY     0.0–1.0 vertical position on PC screen
     * @param pointerId Multi-touch finger ID (0 = primary)
     */
    fun send(type: String, normX: Float, normY: Float, pointerId: Int = 0) {
        // Format floats with 4 decimal places to keep JSON payload small
        val body = """{"Type":"$type","NormX":${"%.4f".format(normX)},"NormY":${"%.4f".format(normY)},"PointerId":$pointerId}"""
        scope.launch {
            runCatching {
                client.newCall(
                    Request.Builder()
                        .url(url)
                        .post(body.toRequestBody(json))
                        .build()
                ).execute().close()
            }
            // Errors silently ignored — input drop is preferable to lag
        }
    }

    fun sendConfig(width: Int, height: Int, refreshRate: Int) {
        val body = """{"Width":$width,"Height":$height,"RefreshRate":$refreshRate}"""
        scope.launch {
            runCatching {
                client.newCall(
                    Request.Builder()
                        .url("http://$host:$port/config")
                        .post(body.toRequestBody(json))
                        .build()
                ).execute().close()
            }
        }
    }

    fun destroy() {
        scope.cancel()
        client.dispatcher.executorService.shutdown()
        client.connectionPool.evictAll()
    }
}

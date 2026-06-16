package com.FOLD

import android.content.Context
import android.graphics.*
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.util.AttributeSet
import android.util.Log
import android.view.SurfaceHolder
import android.view.SurfaceView
import kotlinx.coroutines.*
import kotlin.coroutines.coroutineContext
import java.io.InputStream
import java.net.Socket
import java.nio.ByteBuffer

private const val TAG  = "H264View"
private const val PORT = 8765

private const val TYPE_SPS   : Byte = 0x01
private const val TYPE_PPS   : Byte = 0x02
private const val TYPE_FRAME : Byte = 0x00
private const val TYPE_RESOLUTION : Byte = 0x03

/**
 * H.264 decoder that bypasses all Surface-mode issues.
 *
 * WHY SURFACE MODE IS SKIPPED:
 *   On many Qualcomm phones, MediaCodec.configure(surface) throws IllegalArgumentException
 *   for ALL decoders (HW and SW alike). Root causes include surface compositor policy,
 *   DRM restrictions, and SurfaceView surface state. Rather than fighting this, we use
 *   ByteBuffer mode which works on 100% of Android devices.
 *
 * RENDERING PIPELINE:
 *   MediaCodec (ByteBuffer, null surface) → getOutputImage() → YUV_420_888 → ARGB bitmap
 *   If getOutputImage() returns null, falls back to raw getOutputBuffer() + manual YUV parse.
 *   Bitmap is blitted to SurfaceView canvas each frame.
 */
class H264SurfaceView @JvmOverloads constructor(
    ctx: Context, attrs: AttributeSet? = null
) : SurfaceView(ctx, attrs), SurfaceHolder.Callback {

    /** Wired by the Activity to show status without touching the video canvas. */
    var statusListener: ((String) -> Unit)? = null
    var statsListener: ((String) -> Unit)? = null

    private val mainScope   = CoroutineScope(Dispatchers.Main + SupervisorJob())
    private var streamJob   : Job?            = null
    private var currentHost : String?         = null
    @Volatile private var frameCount = 0
    private var streamWidth  = 1280
    private var streamHeight = 720

    init { holder.addCallback(this) }

    // ── Public API ────────────────────────────────────────────────────────────

    fun start(host: String, w: Int, h: Int) {
        currentHost  = host
        streamWidth  = w
        streamHeight = h
        if (holder.surface?.isValid == true) launchStream(host)
    }

    fun stop() {
        streamJob?.cancel()
        streamJob = null
    }

    // ── SurfaceHolder.Callback ────────────────────────────────────────────────

    override fun surfaceCreated(h: SurfaceHolder) { currentHost?.let { launchStream(it) } }
    override fun surfaceChanged(h: SurfaceHolder, fmt: Int, w: Int, ht: Int) {}
    override fun surfaceDestroyed(h: SurfaceHolder) { stop() }

    // ── Stream loop ───────────────────────────────────────────────────────────

    private fun launchStream(host: String) {
        frameCount = 0
        val oldJob = streamJob
        streamJob = mainScope.launch {
            if (oldJob != null) {
                try {
                    oldJob.cancelAndJoin()
                } catch (e: Exception) {
                    Log.w(TAG, "Failed to cancel old job: ${e.message}")
                }
            }
            withContext(Dispatchers.IO) {
                while (isActive) {
                    try   { streamSession(host) }
                    catch (e: CancellationException) { break }
                    catch (e: Exception) {
                        Log.e(TAG, "Session error: ${e.message}", e)
                        postStatus("ERROR: ${e.javaClass.simpleName}\n${e.message}\n\nReconnecting…")
                        delay(2_000)
                    }
                }
            }
        }
    }

    // ── Decode session ────────────────────────────────────────────────────────

    private suspend fun streamSession(host: String) {
        postStatus("Connecting to $host:$PORT …")

        val socket = Socket()
        coroutineContext[Job]?.invokeOnCompletion {
            runCatching { socket.close() }
        }
        socket.connect(java.net.InetSocketAddress(host, PORT), 3000)

        socket.use {
            socket.tcpNoDelay        = true
            socket.receiveBufferSize = 4 * 1024 * 1024
            val stream = socket.getInputStream()

            val resData = readTypedPacket(stream, TYPE_RESOLUTION) ?: error("No Resolution Info")
            val w = readInt(resData.sliceArray(0..3))
            val h = readInt(resData.sliceArray(4..7))
            streamWidth = w
            streamHeight = h
            Log.d(TAG, "Resolution updated from server: ${w}x${h}")

            val sps = readTypedPacket(stream, TYPE_SPS) ?: error("No SPS")
            val pps = readTypedPacket(stream, TYPE_PPS) ?: error("No PPS")
            Log.d(TAG, "SPS=${sps.size}B  PPS=${pps.size}B")

            postStatus("Config received. Starting decoder…")

            // Align to 16 pixels (standard macroblock matching what the server sends)
            val alignedW = (streamWidth / 16) * 16
            val alignedH = (streamHeight / 16) * 16

            val format = MediaFormat.createVideoFormat(
                MediaFormat.MIMETYPE_VIDEO_AVC, alignedW, alignedH
            ).apply {
                setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, 8 * 1024 * 1024)
                setInteger(MediaFormat.KEY_FRAME_RATE, 120) // Hint 120Hz to Qualcomm power profile
                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
                    setInteger(MediaFormat.KEY_LOW_LATENCY, 1) // Android 11+ zero-queue low latency mode
                }
                setByteBuffer("csd-0", ByteBuffer.wrap(sps))
                setByteBuffer("csd-1", ByteBuffer.wrap(pps))
            }

            val decoder = MediaCodec.createDecoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
            Log.d(TAG, "Decoder: ${decoder.name}")

            try {
                // ── Surface mode (zero-copy GPU rendering) ────────────────────────
                // c2.* (Codec 2.0) decoders support Surface mode reliably.
                // OMX decoders on some MIUI devices throw IllegalArgumentException;
                // we catch that and fall back to ByteBuffer automatically.
                val liveSurface = holder.surface?.takeIf { it.isValid }
                val usingSurface: Boolean = if (liveSurface != null) {
                    try {
                        decoder.configure(format, liveSurface, null, 0)
                        Log.d(TAG, "Configured in Surface mode (zero-copy)")
                        true
                    } catch (e: Exception) {
                        Log.w(TAG, "Surface mode failed (${e.message}) — falling back to ByteBuffer")
                        decoder.reset()
                        decoder.configure(format, null, null, 0)
                        false
                    }
                } else {
                    Log.w(TAG, "Surface not ready — using ByteBuffer mode")
                    decoder.configure(format, null, null, 0)
                    false
                }

                decoder.start()
                Log.d(TAG, "Decoder started in ${if (usingSurface) "Surface" else "ByteBuffer"} mode")

                val info = MediaCodec.BufferInfo()
                val lenBuf = ByteArray(4)
                frameCount = 0
                var outputFormatWidth = alignedW
                var outputFormatHeight = alignedH

                // Stats trackers
                var bytesThisSecond = 0L
                var framesThisSecond = 0
                var lastStatsTime = System.currentTimeMillis()

                while (coroutineContext[Job]?.isActive == true) {
                    val typeByte = stream.read()
                    if (typeByte < 0) break

                    readFully(stream, lenBuf)
                    val len = readInt(lenBuf)
                    require(len in 1..50_000_000) { "Bad frame length: $len" }

                    val data = ByteArray(len)
                    readFully(stream, data)

                    // Track received bytes (len + 1 type byte + 4 length bytes)
                    bytesThisSecond += len + 5

                    if (typeByte.toByte() != TYPE_FRAME) {
                        Log.w(TAG, "Skipping non-frame packet 0x${typeByte.toString(16)}")
                        continue
                    }

                    // Feed to decoder (wait/retry if decoder input queue is full)
                    var inIdx = decoder.dequeueInputBuffer(10000) // 10ms wait
                    while (inIdx < 0 && coroutineContext[Job]?.isActive == true) {
                        inIdx = decoder.dequeueInputBuffer(10000)
                    }
                    if (inIdx >= 0) {
                        decoder.getInputBuffer(inIdx)!!.apply {
                            clear()
                            put(data)
                        }
                        decoder.queueInputBuffer(inIdx, 0, data.size, System.nanoTime() / 1_000L, 0)
                    }

                    // Drain output buffers
                    var outIdx = decoder.dequeueOutputBuffer(info, 0)
                    while (outIdx != MediaCodec.INFO_TRY_AGAIN_LATER) {
                        when {
                            outIdx == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED -> {
                                val fmt = decoder.outputFormat
                                outputFormatWidth = fmt.getInteger(MediaFormat.KEY_WIDTH)
                                outputFormatHeight = fmt.getInteger(MediaFormat.KEY_HEIGHT)
                                if (fmt.containsKey("crop-left")) {
                                    val cw = fmt.getInteger("crop-right") - fmt.getInteger("crop-left") + 1
                                    val ch = fmt.getInteger("crop-bottom") - fmt.getInteger("crop-top") + 1
                                    outputFormatWidth = cw
                                    outputFormatHeight = ch
                                }
                                Log.d(TAG, "Output format changed: ${outputFormatWidth}x${outputFormatHeight}")
                                if (frameCount == 0) postStatus(null)
                            }
                            outIdx >= 0 -> {
                                if (usingSurface) {
                                    decoder.releaseOutputBuffer(outIdx, true)
                                } else {
                                    val image = try { decoder.getOutputImage(outIdx) } catch (e: Exception) { null }
                                    if (image != null) {
                                        try { renderImage(image) } finally { image.close() }
                                        decoder.releaseOutputBuffer(outIdx, false)
                                    } else {
                                        val buf = decoder.getOutputBuffer(outIdx)
                                        if (buf != null) {
                                            renderRawBuffer(buf, outputFormatWidth, outputFormatHeight, decoder.outputFormat)
                                        }
                                        decoder.releaseOutputBuffer(outIdx, false)
                                    }
                                }
                                frameCount++
                                framesThisSecond++
                                if (frameCount == 1) postStatus(null)
                                if (frameCount <= 5 || frameCount % 60 == 0) {
                                    Log.d(TAG, "Frame $frameCount rendered (${if (usingSurface) "Surface" else "ByteBuffer"})")
                                }
                            }
                        }
                        outIdx = decoder.dequeueOutputBuffer(info, 0)
                    }

                    // Calculate and publish stats every second
                    val now = System.currentTimeMillis()
                    val elapsed = now - lastStatsTime
                    if (elapsed >= 1000) {
                        val mbps = (bytesThisSecond * 8.0) / (elapsed / 1000.0) / 1_000_000.0
                        val fps = (framesThisSecond * 1000.0) / elapsed
                        val statsStr = String.format(
                            java.util.Locale.US,
                            "%dx%d @ %.0f FPS | %.1f Mbps",
                            outputFormatWidth,
                            outputFormatHeight,
                            fps,
                            mbps
                        )
                        withContext(Dispatchers.Main) {
                            statsListener?.invoke(statsStr)
                        }
                        bytesThisSecond = 0
                        framesThisSecond = 0
                        lastStatsTime = now
                    }
                }
            } finally {
                withContext(Dispatchers.Main) {
                    statsListener?.invoke("")
                }
                try {
                    decoder.stop()
                } catch (e: Exception) {
                    Log.w(TAG, "Decoder stop failed: ${e.message}")
                }
                try {
                    decoder.release()
                    Log.d(TAG, "Decoder released after $frameCount frames")
                } catch (e: Exception) {
                    Log.e(TAG, "Decoder release failed: ${e.message}")
                }
            }
        }
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private var bmp: Bitmap? = null
    private var argbBuf: IntArray? = null
    private var uvCache: IntArray? = null

    /** Path A: getOutputImage() returned a YUV_420_888 Image. */
    private fun renderImage(image: android.media.Image) {
        val w = image.width; val h = image.height
        ensureBuffers(w, h)

        val yP = image.planes[0]; val uP = image.planes[1]; val vP = image.planes[2]
        val yBuf = yP.buffer; val uBuf = uP.buffer; val vBuf = vP.buffer
        val yRS = yP.rowStride; val uvRS = uP.rowStride; val uvPS = uP.pixelStride
        val uvCols = w / 2
        var uvRowCached = -1

        for (row in 0 until h) {
            val uvRow = row ushr 1
            if (uvRow != uvRowCached) {
                uvRowCached = uvRow
                val uvBase = uvRow * uvRS
                for (col in 0 until uvCols) {
                    val src = uvBase + col * uvPS
                    uvCache!![col * 2]     = (uBuf.get(src).toInt() and 0xFF) - 128
                    uvCache!![col * 2 + 1] = (vBuf.get(src).toInt() and 0xFF) - 128
                }
            }
            val yBase = row * yRS; val argBase = row * w
            for (col in 0 until w) {
                val yv = ((yBuf.get(yBase + col).toInt() and 0xFF) - 16) * 298 + 128
                val u  = uvCache!![(col ushr 1) * 2]
                val v  = uvCache!![(col ushr 1) * 2 + 1]
                val r  = ((yv + 409 * v) ushr 8).coerceIn(0, 255)
                val g  = ((yv - 100 * u - 208 * v) ushr 8).coerceIn(0, 255)
                val b  = ((yv + 516 * u) ushr 8).coerceIn(0, 255)
                argbBuf!![argBase + col] = (0xFF shl 24) or (r shl 16) or (g shl 8) or b
            }
        }
        bmpToCanvas(w, h)
    }

    /**
     * Path B: getOutputImage() returned null — parse raw ByteBuffer.
     * Handles YUV420Planar (I420), YUV420SemiPlanar (NV12/NV21), and packed formats.
     */
    private fun renderRawBuffer(buf: ByteBuffer, w: Int, h: Int, fmt: MediaFormat) {
        val colorFmt = fmt.getInteger(MediaFormat.KEY_COLOR_FORMAT, -1)
        val stride   = if (fmt.containsKey(MediaFormat.KEY_STRIDE)) fmt.getInteger(MediaFormat.KEY_STRIDE) else w
        val sliceH   = if (fmt.containsKey(MediaFormat.KEY_SLICE_HEIGHT)) fmt.getInteger(MediaFormat.KEY_SLICE_HEIGHT) else h

        ensureBuffers(w, h)
        buf.rewind()
        val bytes = ByteArray(buf.remaining())
        buf.get(bytes)

        when (colorFmt) {
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Planar,
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible -> {
                // I420: Y plane, then U plane, then V plane
                yuvPlanarToArgb(bytes, w, h, stride, sliceH, uFirst = true)
            }
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar,
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420PackedSemiPlanar,
            0x7FA30C04 /* NV12 Qualcomm */ -> {
                // NV12: Y plane, then interleaved UV
                nv12ToArgb(bytes, w, h, stride, sliceH, uFirst = true)
            }
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYCrYCb,
            MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420PackedPlanar,
            0x7FA30C03 /* NV21 Qualcomm */ -> {
                nv12ToArgb(bytes, w, h, stride, sliceH, uFirst = false)
            }
            else -> {
                // Unknown — try I420 as best guess
                Log.w(TAG, "Unknown color format $colorFmt, trying I420")
                yuvPlanarToArgb(bytes, w, h, stride, sliceH, uFirst = true)
            }
        }
        bmpToCanvas(w, h)
    }

    private fun yuvPlanarToArgb(bytes: ByteArray, w: Int, h: Int, stride: Int, sliceH: Int, uFirst: Boolean) {
        val ySize  = stride * sliceH
        val uvSize = (stride / 2) * (sliceH / 2)
        val uOff   = if (uFirst) ySize else ySize + uvSize
        val vOff   = if (uFirst) ySize + uvSize else ySize
        for (row in 0 until h) {
            for (col in 0 until w) {
                val y = (bytes[row * stride + col].toInt() and 0xFF) - 16
                val u = (bytes[uOff + (row / 2) * (stride / 2) + col / 2].toInt() and 0xFF) - 128
                val v = (bytes[vOff + (row / 2) * (stride / 2) + col / 2].toInt() and 0xFF) - 128
                argbBuf!![row * w + col] = yuvToArgb(y, u, v)
            }
        }
    }

    private fun nv12ToArgb(bytes: ByteArray, w: Int, h: Int, stride: Int, sliceH: Int, uFirst: Boolean) {
        val uvBase = stride * sliceH
        for (row in 0 until h) {
            for (col in 0 until w) {
                val y   = (bytes[row * stride + col].toInt() and 0xFF) - 16
                val uvI = uvBase + (row / 2) * stride + (col / 2) * 2
                val u   = (bytes[uvI + if (uFirst) 0 else 1].toInt() and 0xFF) - 128
                val v   = (bytes[uvI + if (uFirst) 1 else 0].toInt() and 0xFF) - 128
                argbBuf!![row * w + col] = yuvToArgb(y, u, v)
            }
        }
    }

    private fun yuvToArgb(y: Int, u: Int, v: Int): Int {
        val yScaled = y * 298 + 128
        val r = ((yScaled + 409 * v) ushr 8).coerceIn(0, 255)
        val g = ((yScaled - 100 * u - 208 * v) ushr 8).coerceIn(0, 255)
        val b = ((yScaled + 516 * u) ushr 8).coerceIn(0, 255)
        return (0xFF shl 24) or (r shl 16) or (g shl 8) or b
    }

    private fun ensureBuffers(w: Int, h: Int) {
        if (argbBuf?.size != w * h) {
            argbBuf = IntArray(w * h)
            bmp     = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
            uvCache = IntArray(w)
        }
    }

    private fun bmpToCanvas(w: Int, h: Int) {
        bmp!!.setPixels(argbBuf!!, 0, w, 0, 0, w, h)
        val canvas = try { holder.lockCanvas() } catch (e: Exception) { null } ?: return
        try {
            canvas.drawBitmap(bmp!!, null, Rect(0, 0, canvas.width, canvas.height), null)
        } finally {
            try { holder.unlockCanvasAndPost(canvas) } catch (_: Exception) { }
        }
    }

    // ── Protocol helpers ──────────────────────────────────────────────────────

    private fun readTypedPacket(stream: InputStream, expected: Byte): ByteArray? {
        while (true) {
            val type = stream.read(); if (type < 0) return null
            val lenBuf = ByteArray(4); readFully(stream, lenBuf)
            val len = readInt(lenBuf)
            if (len <= 0 || len > 200_000_000) { Log.w(TAG, "Bad len $len"); return null }
            if (type.toByte() == expected) { val d = ByteArray(len); readFully(stream, d); return d }
            Log.w(TAG, "Skip type=0x${type.toByte().toHexStr()} len=$len")
            skipBytes(stream, len.toLong())
        }
    }

    private fun skipBytes(stream: InputStream, n: Long) {
        var rem = n; val buf = ByteArray(65_536)
        while (rem > 0) { val r = stream.read(buf, 0, minOf(buf.size.toLong(), rem).toInt()); if (r < 0) error("EOS"); rem -= r }
    }

    private fun readFully(stream: InputStream, buf: ByteArray) {
        var off = 0
        while (off < buf.size) { val n = stream.read(buf, off, buf.size - off); check(n >= 0) { "EOS" }; off += n }
    }

    private fun readInt(b: ByteArray) =
        ((b[0].toInt() and 0xFF) shl 24) or ((b[1].toInt() and 0xFF) shl 16) or
        ((b[2].toInt() and 0xFF) shl  8) or  (b[3].toInt() and 0xFF)

    // ── Status ────────────────────────────────────────────────────────────────

    private suspend fun postStatus(msg: String?) = withContext(Dispatchers.Main) {
        statusListener?.invoke(msg ?: "")
    }

    // ── Extensions ────────────────────────────────────────────────────────────

    private fun Byte.toHexStr() = (toInt() and 0xFF).toString(16).padStart(2, '0')

    private fun ByteArray.stripStartCode(): ByteArray = when {
        size >= 4 && this[0] == 0.toByte() && this[1] == 0.toByte() &&
                     this[2] == 0.toByte() && this[3] == 1.toByte() -> copyOfRange(4, size)
        size >= 3 && this[0] == 0.toByte() && this[1] == 0.toByte() &&
                     this[2] == 1.toByte()                          -> copyOfRange(3, size)
        else -> this
    }
}

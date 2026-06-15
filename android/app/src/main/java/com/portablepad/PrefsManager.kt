package com.FOLD

import android.content.Context

object PrefsManager {
    private const val FILE    = "FOLD_prefs"
    private const val KEY_IP  = "last_ip"
    private const val KEY_MODE = "last_mode"   // ⚡ USB

    fun getLastIp(ctx: Context): String =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getString(KEY_IP, "") ?: ""

    fun saveLastIp(ctx: Context, ip: String) =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putString(KEY_IP, ip).apply()

    fun getLastMode(ctx: Context): String =                             // ⚡ USB
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE)
            .getString(KEY_MODE, MainActivity.MODE_WIFI) ?: MainActivity.MODE_WIFI

    fun saveLastMode(ctx: Context, mode: String) =                     // ⚡ USB
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putString(KEY_MODE, mode).apply()
}

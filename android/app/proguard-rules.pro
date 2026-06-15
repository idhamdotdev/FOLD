# Add project specific ProGuard rules here.

# Keep OkHttp (used by MjpegView and TouchSender)
-dontwarn okhttp3.**
-dontwarn okio.**
-keep class okhttp3.** { *; }
-keep interface okhttp3.** { *; }

# Keep all app classes (small app — no need to aggressively shrink)
-keep class com.portablepad.** { *; }

# Keep ViewBinding generated classes
-keep class com.portablepad.databinding.** { *; }

# Kotlin serialization / coroutines
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
-keep class kotlin.coroutines.** { *; }
-keep class kotlinx.coroutines.** { *; }

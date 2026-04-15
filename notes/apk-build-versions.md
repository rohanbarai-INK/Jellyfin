# APK Build Versions

This document explains how to build two APK versions for KnightFlix Android app.

## APK Versions

### 1. Production APK (for real device testing)
- **Package:** `org.knightflix.mobile.debug`
- **Server URL:** `https://jellyfin.baraibrothers.ink/`
- **Build Type:** Debug (proprietary)
- **Version:** 0.0.1
- **Use Case:** Testing on real physical devices with production server

### 2. Emulator APK (for Android Studio emulator)
- **Package:** `org.knightflix.mobile.debug`
- **Server URL:** `http://10.0.2.2:8097/`
- **Build Type:** Debug (proprietary)
- **Version:** 0.0.1
- **Use Case:** Testing on Android Studio emulator with local dev server

## Building Production APK

```bash
cd jellyfin-android

# Temporarily set production URL in app/build.gradle.kts
# Change line 44 from "http://10.0.2.2:8097/" to "https://jellyfin.baraibrothers.ink/"

# Build
.\gradlew.bat assembleProprietaryDebug --no-daemon

# APK location:
# app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.1-proprietary-debug.apk

# Revert the URL change in app/build.gradle.kts after building
```

## Building Emulator APK

```bash
cd jellyfin-android

# Ensure app/build.gradle.kts has "http://10.0.2.2:8097/" on line 44 (default)

# Build
.\gradlew.bat assembleProprietaryDebug --no-daemon

# APK location:
# app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.1-proprietary-debug.apk
```

## Quick Build Commands

### Production APK (with URL override)
```bash
cd jellyfin-android
.\gradlew.bat assembleProprietaryDebug -P"jellyfin.server.url=https://jellyfin.baraibrothers.ink/" --no-daemon
```

### Emulator APK (default)
```bash
cd jellyfin-android
.\gradlew.bat assembleProprietaryDebug --no-daemon
```

## Installing on Device

### Install via WiFi ADB
```bash
# Ensure phone is connected via WiFi ADB
adb -s 192.168.1.49:5555 install -r "jellyfin-android\app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.1-proprietary-debug.apk"
```

### Install via USB
```bash
adb devices
adb install -r "jellyfin-android\app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.1-proprietary-debug.apk"
```

## Current Configuration

- **Package Name:** `org.knightflix.mobile`
- **Debug Suffix:** `.debug` (added automatically for debug builds)
- **Full Package (Debug):** `org.knightflix.mobile.debug`
- **App Display Name:** KnightFlix
- **Version:** 0.0.1 (defined in `gradle.properties`)

## Server URL Configuration

The server URL is defined in `jellyfin-android/app/build.gradle.kts`:

```kotlin
val serverUrl = (project.findProperty("jellyfin.server.url") as? String)
    ?: System.getenv("SERVER_URL")
    ?: "http://10.0.2.2:8097/"
```

Priority:
1. Gradle property: `-P"jellyfin.server.url=<url>"`
2. Environment variable: `SERVER_URL`
3. Default: `http://10.0.2.2:8097/` (emulator)

## Important Notes

- Always build with the latest code before creating APKs
- The `proprietary` flavor is the default and includes Chromecast support
- Debug APKs are signed with debug keystore and can be installed on real devices
- Release APKs require signing and are not covered in this document

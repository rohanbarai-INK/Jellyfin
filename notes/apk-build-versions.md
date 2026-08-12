# APK Build Versions

This document explains how to build APK versions for KnightFlix Android and Android TV apps.

## APK Versions

## Latest Shared APK Location

```text
_deploy\apk\v0.0.3\KnightFlixTV-v0.0.3-debug.apk
_deploy\apk\v0.0.3\KnightFlixMobile-v0.0.3-proprietary-debug.apk
```

### 1. Production APK (for real device testing)
- **Package:** `org.knightflix.mobile.debug`
- **Primary Server URL:** `https://knightflix.in/`
- **Fallback Server URL:** `http://192.168.1.7:8097/`
- **Build Type:** Debug (proprietary)
- **Version:** 0.0.3
- **Use Case:** Testing on real physical devices with production server

### 2. Emulator APK (for Android Studio emulator)
- **Package:** `org.knightflix.mobile.debug`
- **Server URL:** override with `-P"jellyfin.server.url=http://10.0.2.2:8097/"`
- **Build Type:** Debug (proprietary)
- **Version:** 0.0.3
- **Use Case:** Testing on Android Studio emulator with local dev server

## Building Production APK

```bash
cd jellyfin-android

# Defaults are now:
# Primary URL: https://knightflix.in/
# Fallback URL: http://192.168.1.7:8097/

# Build
.\gradlew.bat assembleProprietaryDebug --no-daemon

# APK location:
# app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.3-proprietary-debug.apk
```

## Building Emulator APK

```bash
cd jellyfin-android

# Build
.\gradlew.bat assembleProprietaryDebug -P"jellyfin.server.url=http://10.0.2.2:8097/" --no-daemon

# APK location:
# app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.3-proprietary-debug.apk
```

## Quick Build Commands

### Production APK (with URL override)
```bash
cd jellyfin-android
.\gradlew.bat assembleProprietaryDebug -P"jellyfin.server.url=https://knightflix.in/" -P"jellyfin.server.fallbackUrl=http://192.168.1.7:8097/" --no-daemon
```

### Emulator APK (default)
```bash
cd jellyfin-android
.\gradlew.bat assembleProprietaryDebug --no-daemon
```

## Building Android TV APK

```bash
cd jellyfin-androidtv
.\gradlew.bat assembleDebug -P"jellyfin.server.url=https://knightflix.in/" -P"jellyfin.server.fallbackUrl=http://192.168.1.7:8097/" --no-daemon
```

The APK is created in:

```text
jellyfin-androidtv\app\build\outputs\apk\debug\KnightFlix-v0.0.3-debug.apk
```

Current Android TV version is `0.0.3` in `jellyfin-androidtv/gradle.properties`; the fallback in `jellyfin-androidtv/buildSrc/src/main/kotlin/VersionUtils.kt` is also `0.0.3`.

## Installing on Device

### Install via WiFi ADB
```bash
# Ensure phone is connected via WiFi ADB
adb -s 192.168.1.49:5555 install -r "jellyfin-android\app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.3-proprietary-debug.apk"
```

### Install via USB
```bash
adb devices
adb install -r "jellyfin-android\app\build\outputs\apk\proprietary\debug\KnightFlix-v0.0.3-proprietary-debug.apk"
```

## Current Configuration

- **Package Name:** `org.knightflix.mobile`
- **Debug Suffix:** `.debug` (added automatically for debug builds)
- **Full Package (Debug):** `org.knightflix.mobile.debug`
- **App Display Name:** KnightFlix
- **Version:** 0.0.3 (defined in `gradle.properties`)

## Server URL Configuration

The server URL is defined in `jellyfin-android/app/build.gradle.kts`:

```kotlin
val serverUrl = (project.findProperty("jellyfin.server.url") as? String)
    ?: System.getenv("SERVER_URL")
    ?: "https://knightflix.in/"
val fallbackServerUrl = (project.findProperty("jellyfin.server.fallbackUrl") as? String)
    ?: System.getenv("SERVER_FALLBACK_URL")
    ?: "http://192.168.1.7:8097/"
```

Priority:
1. Gradle property: `-P"jellyfin.server.url=<url>"`
2. Environment variable: `SERVER_URL`
3. Default primary: `https://knightflix.in/`

Fallback priority:
1. Gradle property: `-P"jellyfin.server.fallbackUrl=<url>"`
2. Environment variable: `SERVER_FALLBACK_URL`
3. Default fallback: `http://192.168.1.7:8097/`

Runtime behavior:
- Android TV checks `https://knightflix.in/` first; if it cannot resolve/connect, it tries `http://192.168.1.7:8097/`.
- Mobile opens `https://knightflix.in/` first; if the WebView initial connection fails, it automatically switches to `http://192.168.1.7:8097/`.

## Important Notes

- Always build with the latest code before creating APKs
- The `proprietary` flavor is the default and includes Chromecast support
- Debug APKs are signed with debug keystore and can be installed on real devices
- Release APKs require signing and are not covered in this document

## Windows Sandbox Build Notes

If Gradle or Android signing fails with access denied under `C:\Users\Barai Brothers\.gradle` or `C:\Users\Barai Brothers\.android`, build with writable homes inside this repo:

```powershell
$env:GRADLE_USER_HOME = "C:\Users\Barai Brothers\Documents\Jellyfin\.gradle-user-home"
$env:GRADLE_RO_DEP_CACHE = "C:\Users\Barai Brothers\.gradle\caches"
$env:ANDROID_USER_HOME = "C:\Users\Barai Brothers\Documents\Jellyfin\.android-user-home"
```

Android TV can use `jellyfin-androidtv\gradlew.bat` after copying or downloading the wrapper distribution into `.gradle-user-home`. Mobile may need the locally cached Gradle 9.3.1 executable if Gradle 9.2.1 is not already cached:

```powershell
& "C:\Users\Barai Brothers\.gradle\wrapper\dists\gradle-9.3.1-bin\23ovyewtku6u96viwx3xl3oks\gradle-9.3.1\bin\gradle.bat" assembleProprietaryDebug -P"jellyfin.server.url=https://knightflix.in/" -P"jellyfin.server.fallbackUrl=http://192.168.1.7:8097/" --no-daemon
```

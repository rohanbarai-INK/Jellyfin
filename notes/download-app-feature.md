# Download App Feature — KnightFlix

## Overview
A yellow pulsing download button appears in the top navbar (between the search/cast buttons and the User Menu avatar). Clicking it shows a floating tooltip with app info and a **Download APK** button. Downloading the APK also opens a 4-step install guide modal.

## Where the Button Shows / Hides

| Environment | Button Shown? |
|---|---|
| Web browser (PC / Desktop) | ✅ Yes |
| Web browser (Android Chrome / Firefox) | ✅ Yes |
| Web browser (iOS Safari / Chrome) | ✅ Yes |
| **Jellyfin Android app (WebView)** | ❌ **No** (auto-detected via `window.NativeShell`) |
| SmartTV / webOS / Tizen browsers | ❌ No (auto-detected) |

Detection is in `DownloadAppButton.tsx` → `isAndroidNativeApp()` and `isTvBrowser()`.

---

## How to Set the APK Download Link (Dropbox)

1. Log in to Dropbox with **steinsgate00007@gmail.com** (via Google login).
2. Upload your APK.
3. Click **Share** on the file and copy the URL.
4. In the copied URL, change the ending parameter from `dl=0` to `dl=1`.
5. Open `jellyfin-web/src/components/toolbar/DownloadAppTooltip.tsx` and set:
   ```ts
   export const APK_DOWNLOAD_URL = '<your_dropbox_link_with_dl=1>';
   export const APK_FILE_NAME    = 'KnightFlix-v0.0.1.apk';
   ```
  keep changing the version number in the filename and the download link for each new release.
---

## How to Change the App Icon in the Tooltip

The icon in the tooltip card is the KnightFlix icon imported from:
```
jellyfin-web/src/assets/branding/icon-transparent.png
```

To swap it, simply replace that PNG file with your new icon (same filename).  
The import in `DownloadAppTooltip.tsx` at the top handles it:
```ts
import appIconUrl from 'assets/branding/icon-transparent.png';
```

---

## Install Guide Screenshots (Updated)

The 4-step popup now uses the real screenshots copied from:
`C:\Users\Barai Brothers\Downloads\mobile-app-download-implementation (1)\public\images`

Current files in this repo:
```
jellyfin-web/src/assets/install-guide/install-step1.png
jellyfin-web/src/assets/install-guide/install-step2.png
jellyfin-web/src/assets/install-guide/install-step3.png
jellyfin-web/src/assets/install-guide/install-step4.png
```

These are imported in:
`jellyfin-web/src/components/toolbar/DownloadAppPopup.tsx`

```ts
import step1Img from 'assets/install-guide/install-step1.png';
import step2Img from 'assets/install-guide/install-step2.png';
import step3Img from 'assets/install-guide/install-step3.png';
import step4Img from 'assets/install-guide/install-step4.png';
```

To replace screenshots in future:
- Keep the same 4 filenames and overwrite the files in `src/assets/install-guide/`, or
- Change both filenames and corresponding imports in `DownloadAppPopup.tsx`.

---

## File Reference

| File | Purpose |
|---|---|
| `src/components/toolbar/DownloadAppButton.tsx` | Main button + platform detection + state management |
| `src/components/toolbar/DownloadAppTooltip.tsx` | Floating tooltip card (APK URL is configured here) |
| `src/components/toolbar/DownloadAppPopup.tsx` | 4-step install guide modal |
| `src/assets/install-guide/install-step1.png` | Install guide screenshot step 1 |
| `src/assets/install-guide/install-step2.png` | Install guide screenshot step 2 |
| `src/assets/install-guide/install-step3.png` | Install guide screenshot step 3 |
| `src/assets/install-guide/install-step4.png` | Install guide screenshot step 4 |
| `src/apps/experimental/components/AppToolbar/index.tsx` | Where the button is placed in the toolbar |
| `src/assets/branding/icon-transparent.png` | KnightFlix icon used in the tooltip |

---

## Placing the APK File

If using a relative URL (`/KnightFlix.apk`), put the file here:
```
jellyfin-web/public/KnightFlix.apk
```

After a production build, it will be served at the root of the web app.

---

## Rebuilding After Changes

```bash
cd jellyfin-web
npm install
npm run build:production
```

Then deploy the `dist/` folder as usual.

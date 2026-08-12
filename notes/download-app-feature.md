# Download App Feature — KnightFlix

## Overview
A yellow pulsing download button appears in the top navbar (between the search/cast buttons and the User Menu avatar). Clicking it shows a floating tooltip with app info and two download buttons — **Mobile App** and **Android TV App**. Downloading either APK also opens a 4-step install guide modal.

For **Android TV users**, this same normal user flow is now supported directly in TV browsers. The download panel becomes larger, centered, and D-pad friendly so users can select the APK using their remote.

## Where the Button Shows / Hides

| Environment | Button Shown? |
|---|---|
| Web browser (PC / Desktop) — **home toolbar** | ✅ Yes |
| Web browser — **login page** | ✅ **Yes** (login-styled Download App action) |
| Web browser (Android Chrome / Firefox) | ✅ Yes |
| Web browser (iOS Safari / Chrome) | ✅ Yes |
| **Android TV browser** | ✅ **Yes** |
| **Jellyfin Android app (WebView)** | ❌ **No** (auto-detected via `window.NativeShell`) |
| SmartTV / webOS / Tizen browsers | ❌ No (auto-detected) |

Detection is in `DownloadAppButton.tsx` / `DownloadAppLoginButton.tsx` and uses the shared browser/layout detection helpers to allow **Android TV** while still hiding the feature on unsupported smart-TV browsers.

### Login page (pre-auth)

Users who cannot reach the homepage (e.g. after an APK URL change / forced update) can still download from `/login`:

- A full-width **Download App** action sits above the Manual Login / Quick Connect buttons.
- Styling matches the login page raised actions, with the yellow KnightFlix download accent.
- The same tooltip + install-guide popup is reused, and **admin-configured APK URLs** are loaded anonymously via `GET /AppDownload/Config`.

## Android TV User Flow

- The navbar download icon remains available for Android TV browser users.
- Press **OK / Enter** on the download icon to open the panel.
- The panel opens in a **centered TV-friendly layout** instead of a small desktop-style dropdown.
- The **Android TV App** button is focused first by default.
- Use the **D-pad**:
  - **Up / Left** → focus **Mobile App**
  - **Down / Right** → focus **Android TV App**
  - **OK / Enter** → trigger the selected download
  - **Back / Escape** → close the panel
- After download starts, the install guide modal still opens as before.

---

## How to Update APK Download Links (Admin Dashboard — No Redeploy Needed)

1. Log in to Dropbox with **steinsgate00007@gmail.com** (via Google login).
2. Upload the new APK(s).
3. Click **Share** on the file and copy the URL.
4. Change the `dl=0` parameter at the end of the URL to `dl=1`.
5. Go to the Admin Dashboard → **App Downloads** (sidebar).
6. Paste the new URL into the **Download URL** field for the relevant app.
7. Update the **APK Filename** to match the new version (e.g. `KnightFlix-v0.0.2.apk`).
8. Check **Show NEW badge** to enable the animated green "NEW" pill on that button so users know a new version is available.
9. Click **Save**.

Changes take effect immediately — no code deployment required.

### Fallback (code-level defaults)

If the admin dashboard has never been saved, the app falls back to the hardcoded constants in:
`jellyfin-web/src/components/toolbar/DownloadAppTooltip.tsx`

```ts
export const APK_DOWNLOAD_URL    = '…';   // Mobile fallback
export const APK_FILE_NAME       = 'KnightFlix-v0.0.1.apk';
export const TV_APK_DOWNLOAD_URL = '…';   // TV fallback
export const TV_APK_FILE_NAME    = 'KnightFlixTV-v0.0.1.apk';
```

The install guide (side-load instructions) is the same for both Mobile and Android TV apps.

## NEW Badge Behaviour

- When **Show NEW badge** is enabled for an app in the admin dashboard, a green animated **NEW** pill appears on that download button inside the tooltip.
- A small green pulsing dot also appears on the toolbar download icon when any app is marked as new.
- The badge automatically stops showing for a user after they click the download button a configurable number of times (per device, tracked in `localStorage`).
- Admin can configure **"Max interactions before badge is hidden"** (default: 3). Set to `1` to show NEW only once per device.
- The interaction counter resets automatically when the admin updates the APK URL — users see the NEW badge again for the new version.
- Disable the badge (uncheck and save) once users have had time to see and download the update.
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
| `src/components/toolbar/DownloadAppButton.tsx` | Main button + platform detection + TV visibility rules + state management |
| `src/components/toolbar/DownloadAppLoginButton.tsx` | Login-page Download App action (pre-auth) |
| `src/components/toolbar/DownloadAppTooltip.tsx` | Floating tooltip card + Android TV-friendly D-pad download panel |
| `src/components/toolbar/DownloadAppPopup.tsx` | 4-step install guide modal |
| `src/controllers/session/login/index.html` | Login page markup (includes download mount) |
| `src/controllers/session/login/index.js` | Mounts/unmounts login download React control |
| `src/controllers/session/login/login.scss` | Login-page download button styles |
| `src/assets/install-guide/install-step1.png` | Install guide screenshot step 1 |
| `src/assets/install-guide/install-step2.png` | Install guide screenshot step 2 |
| `src/assets/install-guide/install-step3.png` | Install guide screenshot step 3 |
| `src/assets/install-guide/install-step4.png` | Install guide screenshot step 4 |
| `src/apps/experimental/components/AppToolbar/index.tsx` | Where the button is placed in the toolbar |
| `src/assets/branding/icon-transparent.png` | KnightFlix icon used in the tooltip |
| `jellyfin/Jellyfin.Api/Controllers/AppDownloadController.cs` | Config API (`GET` is anonymous for login-page access) |
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

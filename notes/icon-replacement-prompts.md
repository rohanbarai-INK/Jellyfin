# Icon Replacement Prompts

## Prompt 1: Square Assets (Width = Height)

```text
Use this source image:
C:\Users\Barai Brothers\Downloads\ZeeResizer (1).png

Task:
Generate and replace all square icon assets using high-quality proportional resize.

Rules:
- Keep transparency (ARGB).
- Do NOT add any background.
- Do NOT crop.
- Do NOT pad.
- Overwrite source asset files only (not build/intermediates).

Square PNG outputs:
- touchicon.png -> 180x180
- touchicon72.png -> 72x72
- touchicon114.png -> 114x114
- touchicon144.png -> 144x144
- touchicon512.png -> 512x512
- icon-transparent.png -> 512x512

Android launcher PNGs:
- mipmap-mdpi -> 48x48
- mipmap-hdpi -> 72x72
- mipmap-xhdpi -> 96x96
- mipmap-xxhdpi -> 144x144
- mipmap-xxxhdpi -> 192x192
(Apply to both ic_launcher.png and ic_launcher_round.png)

Android TV app_icon PNGs:
- 80x80
- 120x120
- 160x160
- 240x240
- 320x320

ICO:
Create a REAL multi-size favicon.ico (not renamed PNG) with sizes:
16, 32, 48, 64, 128, 256

Validate:
1) All dimensions exact.
2) Alpha preserved.
3) ICO header valid (00 00 01 00), count=6.
4) Web and branding mirror files are byte-identical where required.
```

### Prompt 1A: Additional Branding-Assets Square Pack (Scanned Sizes)

```text
Use this source image:
C:\Users\Barai Brothers\Pictures\ChatGPT_Image_Feb_23__2026__07_07_23_PM-removebg-preview.png

Task:
Replace the following square assets using exact target sizes from current files.

Rules:
- Keep transparency (ARGB).
- Do NOT add background.
- Do NOT crop.
- Do NOT pad.
- Resize proportionally to exact target dimensions.
- Overwrite source files only.

Scanned target sizes (replace exactly):
- branding-assets/android/ic_launcher-48.png -> 48x48
- branding-assets/android/ic_launcher-72.png -> 72x72
- branding-assets/android/ic_launcher-96.png -> 96x96
- branding-assets/android/ic_launcher-144.png -> 144x144
- branding-assets/android/ic_launcher-192.png -> 192x192
- branding-assets/androidtv/app_icon-80.png -> 80x80
- branding-assets/androidtv/app_icon-120.png -> 120x120
- branding-assets/android/icon-1024.png -> 1024x1024
- branding-assets/androidtv/app_icon-160.png -> 160x160
- branding-assets/androidtv/app_icon-240.png -> 240x240
- branding-assets/androidtv/app_icon-320.png -> 320x320
- branding-assets/androidtv/icon-1024.png -> 1024x1024
- branding-assets/base/icon-square-1024.png -> 1024x1024
- branding-assets/desktop/icon.png -> 256x256
- branding-assets/desktop/icon-1024.png -> 1024x1024
- branding-assets/desktop/icon.svg -> 250x250 viewport (regenerate SVG content from resized source)

Notes:
- Input list had a typo: `branding-assets/androidtv/app_icon-320gpng`.
- Correct file is: `branding-assets/androidtv/app_icon-320.png`.

Validate:
1) Every PNG output has exact dimensions listed above.
2) Transparency is preserved.
3) `icon.svg` remains 250x250 and renders the new icon.
```

## Prompt 2: Non-Square Assets (Width ≠ Height)

```text
Use this source image:
<PUT_SOURCE_IMAGE_PATH_HERE>

Task:
Generate and replace rectangular assets where width and height differ.

Rules:
- Keep transparency (ARGB).
- Do NOT add any background.
- Preserve target aspect ratio exactly.
- Use center-crop only when source aspect ratio does not match target.
- If source already matches target ratio, resize directly.

Target outputs (example 16:9 banner set):
- app_banner.png -> 160x90
- app_banner.png -> 240x135
- app_banner.png -> 320x180
- app_banner.png -> 480x270
- app_banner.png -> 640x360

Optional web banners:
- banner-light.png -> 1200x300
- banner-dark.png -> 1200x300

Validate:
1) Every output has exact target size.
2) Ratio is correct (for 16:9 set, all remain 16:9).
3) No distortion.
4) Alpha preserved and no solid background introduced.
5) Replace source files only, then rebuild to regenerate intermediates.
```

### Prompt 2A: Banner Source Non-Square Pack (Scanned Sizes)

```text
Use this source image:
C:\Users\Barai Brothers\Pictures\banner.png

Task:
Replace the listed non-square assets with exact original target sizes.

Rules:
- Keep transparency (ARGB).
- Do NOT add solid background.
- Do NOT stretch.
- Use center-crop to target ratio, then resize to exact dimensions.
- Overwrite only source files (no build/intermediates).

Scanned target files and sizes:
- branding-assets/androidtv/app_logo.png -> 252x72 (ratio 3.5000)
- branding-assets/android/app_logo.png -> 252x72 (ratio 3.5000)
- branding-assets/androidtv/banner-320x180.png -> 320x180 (ratio 1.7778, near 16:9)
- branding-assets/base/banner-320x180.png -> 320x180 (ratio 1.7778, near 16:9)
- branding-assets/base/logo-strip.png -> 252x72 (ratio 3.5000)
- branding-assets/base/splash-3086x1000.png -> 3086x1000 (ratio 3.0860)
- branding-assets/base/test-resize.png -> 252x72 (ratio 3.5000)
- branding-assets/base/logo-wide.png -> 1302x378 (ratio 3.4444)
- branding-assets/base/zee-source.png -> 1600x457 (ratio 3.5011)
- branding-assets/desktop/splash.png -> 3086x1000 (ratio 3.0860)
- jellyfin-android/app/src/main/res/drawable-hdpi/app_logo.png -> 378x108 (ratio 3.5000)
- jellyfin-android/app/src/main/res/drawable-mdpi/app_logo.png -> 252x72 (ratio 3.5000)
- jellyfin-android/app/src/main/res/drawable-xhdpi/app_logo.png -> 504x144 (ratio 3.5000)
- jellyfin-android/app/src/main/res/drawable-xxhdpi/app_logo.png -> 756x216 (ratio 3.5000)
- jellyfin-android/app/src/main/res/drawable-xxxhdpi/app_logo.png -> 1008x288 (ratio 3.5000)
- jellyfin-desktop/native/logo.png -> 1302x378 (ratio 3.4444)

SVG targets (regenerate with embedded PNG at matching viewport):
- branding-assets/desktop/splash.svg -> 3086x1000 (viewBox 0 0 3086 1000)
- jellyfin-desktop/resources/images/splash.svg -> 3086x1000 (viewBox 0 0 3086 1000)

Validation:
1) All output dimensions are exact.
2) Alpha channel is preserved.
3) No geometric distortion (crop+resize only).
4) SVG width/height/viewBox match target dimensions.
```

## Prompt 3: For App Icon

```text
Use this source image:
C:\Users\Barai Brothers\Downloads\ChatGPT Image Feb 24, 2026, 03_18_17 AM.png

Task:
Keep XML drawables as fallback wrappers, but back them with density-specific PNG files.

Do not remove these XML files:
- jellyfin-androidtv/app/src/main/res/drawable/ic_jellyfin.xml
- jellyfin-android/app/src/main/res/drawable/ic_splash.xml
- jellyfin-androidtv/app/src/main/res/drawable/app_icon_foreground.xml
- jellyfin-androidtv/app/src/main/res/drawable/app_icon_foreground_monochrome.xml

Rules:
- Keep transparency (ARGB).
- Do NOT add background.
- Do NOT crop.
- Do NOT pad.
- Generate PNGs by exact dp-to-density conversion.
- Keep XML resource names unchanged and update XML internals to reference PNGs.
- Avoid drawable name collisions by using `_png` suffix for generated bitmap resource names.

DP to PX conversion:
- 24dp -> mdpi 24, hdpi 36, xhdpi 48, xxhdpi 72, xxxhdpi 96
- 108dp -> mdpi 108, hdpi 162, xhdpi 216, xxhdpi 324, xxxhdpi 432

Generated PNG names:
- ic_jellyfin_png.png (24dp set)
- ic_splash_png.png (108dp set)
- app_icon_foreground_png.png (108dp set)
- app_icon_foreground_monochrome_png.png (108dp set)

Output folders:
- drawable-mdpi
- drawable-hdpi
- drawable-xhdpi
- drawable-xxhdpi
- drawable-xxxhdpi

XML wrapper mapping:
- ic_jellyfin.xml -> @drawable/ic_jellyfin_png
- ic_splash.xml -> @drawable/ic_splash_png
- app_icon_foreground.xml -> @drawable/app_icon_foreground_png
- app_icon_foreground_monochrome.xml -> @drawable/app_icon_foreground_monochrome_png

Validate:
1) All PNG dimensions are exact per density table.
2) No duplicate resource collision with XML names.
3) XML resource IDs remain the same and still resolve in app code.
4) Android and Android TV `assembleDebug` both pass.
```

## Prompt 4: For App Icon Size Tuning (Android Launcher)

```text
Use this source image:
C:\Users\Barai Brothers\Pictures\ChatGPT_Image_Feb_23__2026__07_07_23_PM-removebg-preview.png

Goal:
Fix launcher icon size when it appears too small/too large, while controlling ring visibility.

Important behavior:
- Adaptive launcher icons may mask/crop edges.
- Legacy mipmap icons may look smaller due to launcher normalization.
- Debug source set can override main icon drawable (must be checked).

Files to include in replacement pass (same source image):
1) Adaptive foreground PNG backing:
- app/src/main/res/drawable-mdpi/ic_launcher_foreground_png.png (108x108)
- app/src/main/res/drawable-hdpi/ic_launcher_foreground_png.png (162x162)
- app/src/main/res/drawable-xhdpi/ic_launcher_foreground_png.png (216x216)
- app/src/main/res/drawable-xxhdpi/ic_launcher_foreground_png.png (324x324)
- app/src/main/res/drawable-xxxhdpi/ic_launcher_foreground_png.png (432x432)

2) Legacy launcher PNGs:
- app/src/main/res/mipmap-mdpi/ic_launcher.png (48x48)
- app/src/main/res/mipmap-hdpi/ic_launcher.png (72x72)
- app/src/main/res/mipmap-xhdpi/ic_launcher.png (96x96)
- app/src/main/res/mipmap-xxhdpi/ic_launcher.png (144x144)
- app/src/main/res/mipmap-xxxhdpi/ic_launcher.png (192x192)
- app/src/main/res/mipmap-mdpi/ic_launcher_round.png (48x48)
- app/src/main/res/mipmap-hdpi/ic_launcher_round.png (72x72)
- app/src/main/res/mipmap-xhdpi/ic_launcher_round.png (96x96)
- app/src/main/res/mipmap-xxhdpi/ic_launcher_round.png (144x144)
- app/src/main/res/mipmap-xxxhdpi/ic_launcher_round.png (192x192)

3) Debug override check (critical):
- app/src/debug/res/drawable/ic_launcher_foreground.xml
  Must reference @drawable/ic_launcher_foreground_png (PNG wrapper), not old vector paths.

Tuning parameters:
- cropFactor controls perceived size (center-crop then resize):
  - 1.00 = full ring visible, usually smaller appearance
  - 0.90 = slightly bigger
  - 0.82 = balanced size (recommended default)
  - 0.70 = very big, likely ring clipping
- verticalShiftFactor (optional) for adaptive foreground placement:
  - e.g. +0.03 to +0.06 if icon must appear lower

Rendering rules:
- Preserve alpha (ARGB).
- High-quality interpolation.
- No solid background.
- Output PNG only.

Adaptive vs legacy mode:
- Adaptive ON:
  Keep both files:
  - app/src/main/res/mipmap-anydpi-v26/ic_launcher.xml
  - app/src/main/res/mipmap-anydpi-v26/ic_launcher_round.xml
- Adaptive OFF (force legacy mipmap icons):
  Remove both files above.

Validation checklist:
1) Build: ./gradlew assembleDebug
2) Install: adb install -r <apk>
3) If icon appears stale, clear launcher cache:
   - adb uninstall org.jellyfin.mobile.debug
   - adb shell pm clear com.google.android.apps.nexuslauncher
   - adb install <apk>
4) Visual verify in:
   - app drawer
   - home screen
   - app info page
```

## Prompt 5 (Serialized: IRP-005): Login Banner + Dual Side Animation (Bottom Left/Right)

```text
Use these source assets:
- Banner: C:\Users\Barai Brothers\Pictures\Prompt 2.png
- Left animation: C:\Users\Barai Brothers\Pictures\anime-dance.webM
- Right animation: C:\Users\Barai Brothers\Pictures\ai-oshino-ko nobg (1).webM

Task:
Set up a branded Jellyfin login screen with:
1) Centered banner above "Please sign in"
2) Two decorative animations at both bottom ends (left and right), near the login button zone

Scope and constraints:
- Only modify jellyfin-web.
- No backend/auth/subscription/API/android-native changes.
- Keep existing login form submission behavior untouched.

Copy assets into repo:
- jellyfin-web/src/assets/branding/login-banner.png
- jellyfin-web/src/assets/branding/anime-left.webm
- jellyfin-web/src/assets/branding/anime-right.webm

Template changes:
- File: jellyfin-web/src/controllers/session/login/index.html
- Add banner block and decoration container above login heading/forms.
- Use bundled paths only:
  - /assets/branding/login-banner.png
  - /assets/branding/anime-left.webm
  - /assets/branding/anime-right.webm
- Video tags must be non-interactive:
  - autoplay muted loop playsinline aria-hidden="true" tabindex="-1"

Visibility logic:
- File: jellyfin-web/src/controllers/session/login/index.js
- Add:
  - isAndroidWebView()
  - isDesktopEnvironment()
  - updateDesktopDecorationsVisibility(view)
- Rule for showing side animations:
  - window.innerWidth >= 1024
  - not mobile browser
  - not TV layout
  - not Android WebView
- Wire lifecycle in SPA mode:
  - update on viewshow
  - listen to window resize
  - remove resize listener on viewhide

Styles:
- File: jellyfin-web/src/controllers/session/login/login.scss
- Banner:
  - centered container
  - responsive image (max-width 280px, width 100%, auto height)
- Decorations:
  - fixed layer, pointer-events none, hidden by default
  - show only with .desktopDecorationsVisible
  - left media pinned bottom-left, right media pinned bottom-right
  - width 220px each
- Ensure login content stays above decorations with proper z-index.

Validation:
1) npm install
2) npm run build:production
3) Desktop (>=1024): banner visible, both side animations visible, inputs clickable
4) Mobile (<1024): banner visible, side animations hidden
5) Android WebView UA: banner visible, side animations hidden
6) git diff --name-only contains only login template/js/scss plus the 3 new branding assets
```

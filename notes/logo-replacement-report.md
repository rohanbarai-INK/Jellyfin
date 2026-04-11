# Jellyfin Logo Replacement Analysis

## Executive Summary

This report identifies all places across the Jellyfin ecosystem where the default Jellyfin logo is being used and can be replaced. The analysis covers four main platforms:

1. Jellyfin Server (backend)
2. Jellyfin Web (frontend)
3. Jellyfin Android (mobile app)
4. Jellyfin Android TV (TV app)
5. Jellyfin Desktop (desktop app)

## 1. Jellyfin Server (Backend)

### Logo Storage and Configuration

**Files:**
- [`jellyfin/MediaBrowser.Model/Branding/BrandingOptions.cs`](jellyfin/MediaBrowser.Model/Branding/BrandingOptions.cs) - Configuration options for branding
- [`jellyfin/Jellyfin.Api/Controllers/BrandingController.cs`](jellyfin/Jellyfin.Api/Controllers/BrandingController.cs) - API endpoint for retrieving branding configuration
- [`jellyfin/Jellyfin.Api/Controllers/ImageController.cs`](jellyfin/Jellyfin.Api/Controllers/ImageController.cs) - API endpoints for image management

**Current Branding Features:**
- **Custom login disclaimer** - Text to display on login screen
- **Custom CSS** - Custom styles for the web interface
- **Custom splash screen** - Image to display during app startup
  - Enabled by default: false
  - Location can be configured
  - API endpoints: `UploadCustomSplashscreen()` and `DeleteCustomSplashscreen()`

**Logo Management Requirements:**
- Add `LogoLocation` property to `BrandingOptions` class
- Add API endpoints for logo management:
  - `UploadCustomLogo()` - To upload a custom logo
  - `DeleteCustomLogo()` - To delete the custom logo
  - `GetCustomLogo()` - To retrieve the custom logo
- Update `BrandingController` to include logo configuration in the API response
- Add logo location validation and storage management
- Ensure backward compatibility - fall back to default logo if custom logo not configured

**Splash Screen Handling:**
- [`jellyfin/Emby.Server.Implementations/Library/SplashscreenPostScanTask.cs`](jellyfin/Emby.Server.Implementations/Library/SplashscreenPostScanTask.cs) - Post-scan task for splash screen
- [`jellyfin/src/Jellyfin.Drawing.Skia/SplashscreenBuilder.cs`](jellyfin/src/Jellyfin.Drawing.Skia/SplashscreenBuilder.cs) - Builds splash screen images
- Web clients fetch splash screen from `/Branding/Splashscreen` endpoint

## 2. Jellyfin Web (Frontend)

### Dashboard Branding Page

**Current Implementation:**
- Located at: [`jellyfin-web/src/apps/dashboard/routes/branding/index.tsx`](jellyfin-web/src/apps/dashboard/routes/branding/index.tsx)
- Current features:
  - Custom splash screen upload/delete
  - Login disclaimer text field
  - Custom CSS text area
  - Save button for changes

**UI Components:**
- Uses Material-UI components (Stack, TextField, Button, Switch, Alert)
- File upload functionality with preview
- Real-time validation and error handling
- Loading states for API calls

**Logo Management UI Requirements:**
- Add logo upload/delete functionality similar to splash screen
- Add logo preview section
- Add logo configuration options (size, position, etc.)
- Update the branding page UI to include logo section
- Add API integration for logo endpoints
- Update the query client to invalidate logo cache

### Logo Files and References

**Main Logo File:**
- Imported from `@jellyfin/ux-web/icon-transparent.png` (npm package)

**Files Using Logo:**
1. [`jellyfin-web/src/plugins/logoScreensaver/plugin.js`](jellyfin-web/src/plugins/logoScreensaver/plugin.js) - Logo screensaver plugin that displays the logo with animations
2. [`jellyfin-web/src/constants/branding.ts`](jellyfin-web/src/constants/branding.ts) - Constants for branding URLs (needs to be updated with logo endpoint)

### Logo Screensaver Details

The logo screensaver plugin:
- Imports the transparent logo from `@jellyfin/ux-web/icon-transparent.png`
- Displays the logo with various animations (bounceInLeft, bounceInRight, swing, tada, wobble, rotateIn, rotateOut)
- Activates after a period of inactivity
- Fades out when deactivated
- Needs to be updated to use custom logo from server if available

### Other Branding References

- [`jellyfin-web/src/index.html`](jellyfin-web/src/index.html) - May contain logo references
- [`jellyfin-web/src/apps/dashboard/`](jellyfin-web/src/apps/dashboard/) - Various dashboard components that may display branding

## 3. Jellyfin Android (Mobile App)

### Logo File:
- [`jellyfin-android/app/src/main/res/drawable/app_logo.xml`](jellyfin-android/app/src/main/res/drawable/app_logo.xml) - Vector drawable logo in XML format

### Files Using Logo:
1. [`jellyfin-android/app/src/main/java/org/jellyfin/mobile/ui/screens/connect/ConnectScreen.kt`](jellyfin-android/app/src/main/java/org/jellyfin/mobile/ui/screens/connect/ConnectScreen.kt) - Displays logo in the header of the server connection screen

**Usage Details:**
- Logo is displayed in a centered row with 72dp height
- Used as the main branding element on the initial connect screen
- Content description is null (accessibility consideration)

## 4. Jellyfin Android TV (TV App)

### Logo Files:
- [`jellyfin-androidtv/app/src/main/res/drawable/app_logo.xml`](jellyfin-androidtv/app/src/main/res/drawable/app_logo.xml) - Vector drawable logo (same as Android app)
- [`jellyfin-androidtv/app/src/main/res/drawable-v24/app_logo.xml`](jellyfin-androidtv/app/src/main/res/drawable-v24/app_logo.xml) - Vector drawable logo for Android 7.0+
- [`jellyfin-androidtv/app/src/main/res/values/logo.xml`](jellyfin-androidtv/app/src/main/res/values/logo.xml) - Logo dimensions and configuration

### Files Using Logo:
1. [`jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/shared/toolbar/Toolbar.kt`](jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/shared/toolbar/Toolbar.kt) - Displays logo in the toolbar
2. [`jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/startup/fragment/SplashFragment.kt`](jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/startup/fragment/SplashFragment.kt) - Displays logo on splash screen
3. [`jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/integration/dream/composable/DreamContentLogo.kt`](jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/integration/dream/composable/DreamContentLogo.kt) - Displays logo in the dream/screensaver mode

**Splash Screen Details:**
- Logo is displayed centered on a dark background
- Width: 400dp, fills max height
- Used during app startup

**Dream/Screensaver Details:**
- Logo is displayed centered on a black background
- Width: 400dp, fills max height
- Shown when device is in idle/dream mode

## 5. Jellyfin Desktop (Desktop App)

### Logo File:
- [`jellyfin-desktop/native/logo.png`](jellyfin-desktop/native/logo.png) - PNG format logo

**Application Icon Files:**
- [`jellyfin-desktop/resources/images/icon.png`](jellyfin-desktop/resources/images/icon.png) - Main application icon
- [`jellyfin-desktop/resources/images/icon.svg`](jellyfin-desktop/resources/images/icon.svg) - Vector format icon
- [`jellyfin-desktop/resources/images/splash.png`](jellyfin-desktop/resources/images/splash.png) - Splash screen image
- [`jellyfin-desktop/resources/images/splash.svg`](jellyfin-desktop/resources/images/splash.svg) - Vector format splash screen

## 6. Additional Logo References

### Live TV Logo Handling
- [`jellyfin/src/Jellyfin.LiveTv/Listings/SchedulesDirectDtos/LogoDto.cs`](jellyfin/src/Jellyfin.LiveTv/Listings/SchedulesDirectDtos/LogoDto.cs) - DTO for handling TV channel logos from Schedules Direct
- [`jellyfin/src/Jellyfin.LiveTv/Listings/SchedulesDirect.cs`](jellyfin/src/Jellyfin.LiveTv/Listings/SchedulesDirect.cs) - Retrieves and processes TV channel logos
- [`jellyfin-web/src/components/channelMapper/channelMapper.js`](jellyfin-web/src/components/channelMapper/channelMapper.js) - Maps TV channels and displays logos

### Image Type Definitions
- [`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Enums/ArtKind.cs`](jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Enums/ArtKind.cs) - Defines image types including logos
- [`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/ImageInfoImageType.cs`](jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/ImageInfoImageType.cs) - Database entity for image types

## Replacement Strategy

### For Custom Branding

#### 1. Server-Side Changes:
   - **BrandingOptions.cs** - Add `LogoLocation` property to store custom logo file path
   - **BrandingController.cs** - Update to include logo configuration in the API response
   - **ImageController.cs** - Add API endpoints:
     - `UploadCustomLogo()` - To upload a custom logo
     - `DeleteCustomLogo()` - To delete the custom logo
     - `GetCustomLogo()` - To retrieve the custom logo
   - **Configuration management** - Add validation and storage management for logo files
   - **Backward compatibility** - Ensure default logo is used if custom logo not configured

#### 2. Web Client Changes:
   - **Branding page UI** - Update [`jellyfin-web/src/apps/dashboard/routes/branding/index.tsx`](jellyfin-web/src/apps/dashboard/routes/branding/index.tsx) to add logo management
   - **API integration** - Add useCustomLogo hook similar to useBrandingOptions
   - **Branding constants** - Update `branding.ts` with logo endpoint
   - **Logo screensaver** - Modify `logoScreensaver/plugin.js` to use custom logo
   - **All logo references** - Update all components that display the logo to check for custom configuration

#### 3. Android App Changes:
   - Replace `app_logo.xml` in drawable folders
   - Ensure compatibility with different screen densities
   - Update any hardcoded dimensions
   - Add logic to fetch and use custom logo from server if available

#### 4. Android TV App Changes:
   - Same as Android app, plus:
     - Update `drawable-v24/app_logo.xml` for Android 7.0+
     - Modify values in `logo.xml`
     - Test on various TV screen sizes
     - Add logic to fetch and use custom logo from server if available

#### 5. Desktop App Changes:
   - Replace `native/logo.png`
   - Update application icon files
   - Replace splash screen images
   - Add logic to fetch and use custom logo from server if available

### Recommended Approach

1. **Create a custom branding package** that includes all logo variations
2. **Implement server-side management UI** for uploading and configuring custom logos
3. **Update all client applications** to respect the custom branding settings
4. **Ensure backward compatibility** with existing installations
5. **Test on various devices** and screen sizes

## 7. Dashboard Branding Page

### Current Features

The branding page is currently implemented at [`jellyfin-web/src/apps/dashboard/routes/branding/index.tsx`](jellyfin-web/src/apps/dashboard/routes/branding/index.tsx) and supports:

**Splash Screen Management:**
- Toggle to enable/disable custom splash screen
- File upload for custom splash screen
- Preview of current splash screen
- Delete custom splash screen
- Information about recommended splash screen size

**Login Disclaimer:**
- Text area for custom login disclaimer
- Multiline support
- Helper text explaining usage

**Custom CSS:**
- Text area for custom CSS
- Syntax highlighting (monospace font)
- Spell check disabled
- Helper text explaining usage

### Planned Logo Management Features

**Logo Upload and Preview:**
- File upload component for custom logo
- Preview of current logo
- Delete custom logo functionality
- Logo format and size validation

**Logo Configuration Options:**
- Logo size control (small, medium, large)
- Logo position (left, center, right in toolbar)
- Logo visibility settings (show/hide in different screens)
- Responsive behavior options

**UI Improvements:**
- Organize branding options into sections (Logo, Splash Screen, Login, CSS)
- Better visual hierarchy
- Improved error handling and validation
- Loading states for file operations

## Files Summary Table

| Platform | File Type | Location | Purpose |
|----------|-----------|----------|---------|
| Server | C# | `BrandingOptions.cs` | Configuration options |
| Server | C# | `BrandingController.cs` | API endpoint |
| Server | C# | `ImageController.cs` | Image management endpoints |
| Web | TypeScript | `apps/dashboard/routes/branding/index.tsx` | Branding page UI |
| Web | TypeScript | `apps/dashboard/features/branding/api/useBrandingOptions.ts` | API hook |
| Web | JavaScript | `logoScreensaver/plugin.js` | Screensaver plugin |
| Web | TypeScript | `branding.ts` | Constants |
| Android | XML | `app/src/main/res/drawable/app_logo.xml` | Main logo |
| Android | Kotlin | `ConnectScreen.kt` | Connect screen display |
| Android TV | XML | `app/src/main/res/drawable/app_logo.xml` | Main logo |
| Android TV | XML | `app/src/main/res/drawable-v24/app_logo.xml` | Android 7.0+ logo |
| Android TV | Kotlin | `Toolbar.kt` | Toolbar display |
| Android TV | Kotlin | `SplashFragment.kt` | Splash screen display |
| Android TV | Kotlin | `DreamContentLogo.kt` | Screensaver display |
| Desktop | PNG | `native/logo.png` | Application logo |
| Desktop | PNG/SVG | `resources/images/` | Icons and splash screen |

## Conclusion

The Jellyfin ecosystem uses consistent branding across all platforms, with the main logo defined in vector format for scalability. The most critical places to update for custom branding are:

1. Server-side branding configuration
2. Web client logo screensaver
3. Mobile and TV app splash screens
4. Application icons

The current branding page already supports custom splash screens, login disclaimers, and custom CSS. To add dynamic logo management, we need to:

1. Update the server-side configuration to include logo properties
2. Add API endpoints for logo upload, deletion, and retrieval
3. Enhance the dashboard branding page with logo management UI
4. Update all clients to respect custom logo configuration

## Implementation Plan

A detailed implementation plan is available in [`dynamic-logo-implementation-plan.md`](dynamic-logo-implementation-plan.md) that includes:

1. **Server-side changes** - Updating BrandingOptions, API endpoints
2. **Web client changes** - Updating the branding page UI and logo references
3. **Mobile and TV app changes** - Updating the Android and Android TV apps
4. **Testing and validation** - Ensuring the feature works correctly
5. **Timeline and risks** - Estimated schedule and potential risks

By following the implementation plan, you can create a cohesive custom branding experience across all Jellyfin platforms where admins can dynamically change the visible logo via the branding page.

# Grace Period System Implementation Report

Date: 2026-02-22

## Scope Completed

Implemented the Grace Period system across:

- `jellyfin` (server)
- `jellyfin-web`
- `jellyfin-android`
- `jellyfin-androidtv`

No changes were made to `jellyfin-desktop`.

All required constraints were preserved:

- No authentication flow changes
- No expiry calculation/stacking logic changes
- No redemption logic changes
- No route duplication or contract-breaking route changes
- Middleware only safely extended

## Server Changes (`jellyfin`)

- Added `GracePeriodDays` (default `3`) in:
  - `jellyfin/MediaBrowser.Model/Configuration/SubscriptionConfiguration.cs`
- Added grace helper methods to:
  - `jellyfin/MediaBrowser.Controller/Security/IAccessKeyService.cs`
- Implemented grace calculation and remaining days in:
  - `jellyfin/Jellyfin.Server.Implementations/Security/AccessKeyService.cs`
- Extended current subscription models and mapping:
  - `jellyfin/MediaBrowser.Controller/Security/CurrentSubscriptionResult.cs`
  - `jellyfin/Jellyfin.Api/Models/AccessKeyDtos/CurrentSubscriptionResponse.cs`
  - `jellyfin/Jellyfin.Api/Controllers/AccessKeyController.cs`
- Extended middleware allow-path for grace users:
  - `jellyfin/Jellyfin.Api/Middleware/ExpiredSubscriptionMiddleware.cs`
- Added grace fields to `Users/Me` DTO and mapping:
  - `jellyfin/MediaBrowser.Model/Dto/UserDto.cs`
  - `jellyfin/Jellyfin.Server.Implementations/Users/UserManager.cs`
- Updated integration tests:
  - `jellyfin/tests/Jellyfin.Server.Integration.Tests/Controllers/SubscriptionFeatureTests.cs`

## Web Changes (`jellyfin-web`)

- Added grace-aware config and user helpers:
  - `jellyfin-web/src/utils/subscription.ts`
- Added/updated tests:
  - `jellyfin-web/src/utils/subscription.test.ts`
- Added grace banner and active-layout handling:
  - `jellyfin-web/src/apps/stable/routes/subscription/index.tsx`
- Added `GracePeriodDays` to dashboard settings save flow:
  - `jellyfin-web/src/apps/dashboard/routes/settings/index.tsx`

## Android Mobile Changes (`jellyfin-android`)

- `/Users/Me` grace parsing and expiry guard update:
  - `jellyfin-android/app/src/main/java/org/jellyfin/mobile/app/ApiClientController.kt`
- Grace fields + banner rendering on subscription screen:
  - `jellyfin-android/app/src/main/java/org/jellyfin/mobile/subscription/SubscriptionActivity.kt`
- Added grace banner layout:
  - `jellyfin-android/app/src/main/res/layout/activity_subscription.xml`
- Added strings/plurals:
  - `jellyfin-android/app/src/main/res/values/strings.xml`

## Android TV Changes (`jellyfin-androidtv`)

- Added `UserAccessState` and grace-aware `/Users/Me` fetch:
  - `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/auth/model/UserExpiry.kt`
- Updated auth guard to block only fully expired users:
  - `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/auth/repository/AuthenticationRepository.kt`
- Updated startup and settings guards:
  - `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/startup/StartupActivity.kt`
  - `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/settings/screen/SettingsMainScreen.kt`
- Updated subscription management screen to parse/render grace:
  - `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/subscription/SubscriptionManagementActivity.kt`
- Added strings/plurals:
  - `jellyfin-androidtv/app/src/main/res/values/strings.xml`

## Issue Found During Testing and Fix

Issue found:

- Android TV `SubscriptionManagementActivity` still had expiry-only handling and did not render grace status/banner.

Fix applied:

- Switched to `UserAccessState` flow for guard logic.
- Added grace parsing from `Keys/CurrentSubscription` (`IsInGracePeriod`, `GraceDaysRemaining`).
- Added grace banner/status rendering and grace remaining display.
- Added `GracePeriodDays` parsing for configured window display.
- Added missing Android TV grace strings/plurals.

Retest result:

- Android TV build and unit test command passed after fix.

## Verification Commands Run

Server:

- `dotnet build` (passed)
- `dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --filter SubscriptionFeatureTests` (passed, 11/11)

Web:

- `npm test -- src/utils/subscription.test.ts` (passed, 13/13)
- `npm run build:check` (passed)

Android Mobile:

- `.\gradlew.bat :app:assembleDebug` (passed)

Android TV:

- `.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest` (passed)

## Testing Preference Recorded

Requested preference for future tasks:

- Always use virtual devices for testing (mobile/TV emulator workflow).


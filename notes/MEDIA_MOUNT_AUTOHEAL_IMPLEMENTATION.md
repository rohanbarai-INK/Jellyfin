# Media Mount Auto-Heal Implementation

## Summary

This document captures the repository-level implementation for media mount drift handling:

- Phase 1: Backend auto-heal status service + API endpoints.
- Phase 2: Frontend global popup integration for reconnecting/recovered/degraded states.

The implementation was developed and validated locally, without deploying to production.

## Backend Implementation

### New model and interface

- `jellyfin/MediaBrowser.Model/System/MediaMountAutoHealStatusInfo.cs`
- `jellyfin/MediaBrowser.Controller/AutoHeal/IMediaMountAutoHealService.cs`

These define a status contract for clients and a service contract for server-side state management.

### New service

- `jellyfin/Jellyfin.Server.Implementations/AutoHeal/MediaMountAutoHealService.cs`

Responsibilities:

- Evaluates media path health (`/media1` and `/media2`, with required subpaths under `/media2`).
- Required subpath rule is tolerant: when `KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS` is configured, at least one required subpath under `/media2` must exist. Auto-heal will not degrade just because one category folder is missing while others are available.
- Persists state in config directory (`autoheal/media-mount-status.json`).
- Maintains lifecycle states:
  - `healthy`
  - `reconnecting`
  - `recovered`
  - `degraded`
- Supports controlled recovery attempts with cooldown and delay.
- Includes Docker restart request path for runtime environments that expose Docker socket.
- Sends Gotify notifications for each restart attempt:
  - Pre-restart notification before Docker restart call.
  - Post-restart notification after attempt completion (success or failure).

### DI registration

- `jellyfin/Jellyfin.Server/CoreAppHost.cs`

Registered service:

- `IMediaMountAutoHealService -> MediaMountAutoHealService`

### API endpoints

- `jellyfin/Jellyfin.Api/Controllers/SystemController.cs`

Added endpoints:

- `GET /System/AutoHeal/Status`

## Frontend Implementation

### Global alert component

- `jellyfin-web/src/components/autoHeal/MediaMountRecoveryAlert.tsx`
- Mounted in `jellyfin-web/src/RootApp.tsx`

Behavior:

- Polls `System/AutoHeal/Status`.
- Shows user-facing English messages:
  - `Media storage is reconnecting. Please wait 30 seconds.`
  - `Playback service has been restored. Please try again.`
  - `Service is temporarily unavailable. Please try again in 1-2 minutes.`
- When API is temporarily unreachable (for example during container restart at app-open), shows:
  - `Server is unavailable, please check after some time.`
- Uses faster polling while reconnecting.
- Uses fast retry polling when status API is unreachable.
- Allows dismiss on all popup states (`reconnecting`, `recovered`, `degraded`, `unavailable`) and keeps it hidden until state payload changes.
- Uses a medium-size popup card with `system-loader.gif` on the left for retrying/unavailable states.

### Android TV transparency overlay

- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/data/service/MediaMountAutoHealStatusService.kt`
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/browsing/MediaMountAutoHealOverlay.kt`
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/ui/browsing/MainActivity.kt`
- `jellyfin-androidtv/app/src/main/res/layout/activity_main.xml`
- `jellyfin-androidtv/app/src/main/java/org/jellyfin/androidtv/di/AppModule.kt`

Behavior:

- Polls `GET /System/AutoHeal/Status` from Android TV when an authenticated session is active.
- Shows a top overlay for:
  - `reconnecting`
  - `recovered`
  - `degraded`
- Falls back to a reconnecting message when the server is temporarily unreachable during restart windows.
- Starts polling on `MainActivity.onStart()` and stops on `MainActivity.onStop()`.

## Local Validation

### Backend tests

Test file:

- `jellyfin/tests/Jellyfin.Server.Tests/AutoHeal/MediaMountAutoHealServiceTests.cs`

Covered cases:

- Missing media paths transition to `degraded` after recovery delay.

### Commands used

```powershell
dotnet test .\jellyfin\tests\Jellyfin.Server.Tests\Jellyfin.Server.Tests.csproj --filter MediaMountAutoHealServiceTests /p:RunAnalyzers=false
```

```powershell
npm run build:production
```

```powershell
cd .\jellyfin-androidtv
.\gradlew :app:compileDebugKotlin
```

## Environment Variables

Supported settings:

- `KNIGHTFLIX_AUTOHEAL_ENABLED` (default: `true`)
- `KNIGHTFLIX_AUTOHEAL_CONTAINER_NAME` (default: `KnightFlix`)
- `KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH` (default: `/var/run/docker.sock`)
- `KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH` (default: `/media1`)
- `KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH` (default: `/media2`)
- `KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS` (default: `Anime;TVSeries;Hollywood`)
- `KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS` (default: `600`)
- `KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS` (default: `30`)
- `KNIGHTFLIX_AUTOHEAL_RECOVERED_BANNER_SECONDS` (default: `45`)
- `KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS` (default: `20`, prevents short drift blips from immediately entering degraded/recovery)
- `KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED` (default: `false`)
- `KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL` (example: `https://gotify.baraibrothers.ink`)
- `KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN` (required when Gotify is enabled)
- `KNIGHTFLIX_AUTOHEAL_GOTIFY_PRIORITY` (default: `7`)

## Gotify Payload

Each restart-triggered notification includes detailed context:

- phase (`PRE_RESTART` or `POST_RESTART_SUCCESS` / `POST_RESTART_FAILED`)
- attempt number
- UTC timestamp
- host name
- container name
- current state
- media paths (`/media1`, `/media2`)
- required `/media2` directories
- failure reason and error summary when available

## Notes

- No production deploy was performed as part of this implementation.
- Actual container restart in production requires runtime Docker control (for example Docker socket access or an equivalent host-side restart mechanism).

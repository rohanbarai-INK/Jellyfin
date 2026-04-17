# Announcement System (Admin-Controlled)

## Purpose

This implementation upgrades the reusable post-login announcement popup from hardcoded campaign config to an admin-managed system.

Admin users can now manage announcement content, media, schedule, limits, status, and CTA behavior directly from Dashboard.

## Dashboard management

- New admin page: `Dashboard -> Announcement`
- New drawer item added alongside existing Request and Subscription Command Center entries.
- Admin-only access via existing dashboard elevation policy.

## What can be controlled

From the Announcement admin page:

- Enable/disable per announcement
- Draft vs Published status
- Preview modal before publish
- Priority and sort order for overlapping campaigns
- Full text control:
  - heading, title, subtitle, description, highlights, help text
  - media caption and alt text
  - CTA label and close label
- Media control:
  - Hero GIF source (builtin token, URL, or uploaded data URL)
  - Main preview image source (builtin token, URL, or uploaded data URL)
- Campaign timing:
  - start date/time
  - end date/time
- Impression limits:
  - per day
  - total per user
- CTA target behavior:
  - internal app route
  - external URL
- Audit fields:
  - created by / created at
  - updated by / updated at

## Popup behavior

- Existing popup visual layout remains unchanged.
- Campaigns are loaded from server (`Announcement/Active`) after sign-in.
- If multiple campaigns are active and eligible, popup supports slide navigation within the same modal.
- Internal CTA uses app routing.
- External CTA redirects to provided URL.

## Eligibility and impression tracking

Client-side eligibility still runs in `FeatureAnnouncementsRoot`.

A campaign is eligible only if:

- enabled
- within start/end window
- under total impression limit (`maxImpressionsPerUser`)
- under per-day impression limit (`maxImpressionsPerDay`)
- not already shown during current session

Storage model:

- `localStorage` key: `jellyfin.featureAnnouncements.v1:<serverId>:<userId>`
  - tracks total impressions + daily impression map per campaign
- `sessionStorage` key: `jellyfin.featureAnnouncements.session.v1:<serverId>:<userId>`
  - tracks campaign ids shown in current session

## Backend API and persistence

### New API controller

- `GET /Announcement/Active` (authorized users)
- `GET /Announcement/Admin` (admin only)
- `POST /Announcement/Admin/Upsert` (admin only)

### New backend service

- `IFeatureAnnouncementService` + `FeatureAnnouncementService`
- Server-side validation includes:
  - invalid date windows
  - invalid CTA targets (internal route or http/https external URL)
  - per-day/total limits > 0
  - campaign id uniqueness
  - media fallback defaults if missing

### DB entity/table

- Entity: `FeatureAnnouncement`
- Table: `FeatureAnnouncements`
- Includes:
  - campaign identity and status
  - all display text/media fields
  - CTA type and target
  - scheduling and impression limits
  - priority/sort
  - audit metadata

## Seeded default campaign

Initial seed preserves original Leaderboard announcement content and media context.

- Campaign id: `leaderboard-launch-2026`
- Status: `Published`
- Enabled: `true`
- Start: `2026-04-17T00:00:00Z`
- End: `2026-04-30T23:59:59Z`
- Limits:
  - 2 impressions per day per user
  - 10 total impressions per user
- CTA:
  - label: `Check It Out`
  - type: internal route
  - target: `/achievements`

Default seeded media sources use builtin tokens:

- `builtin:request-popup-accent`
- `builtin:leaderboard-announcement-preview`

The web layer resolves builtin tokens to bundled assets.

## Frontend files added/updated

### New

- `jellyfin-web/src/utils/featureAnnouncementsApi.ts`
- `jellyfin-web/src/apps/dashboard/features/announcement/index.tsx`
- `jellyfin-web/src/apps/dashboard/features/announcement/announcement.scss`
- `jellyfin-web/src/apps/dashboard/routes/announcement/index.tsx`

### Updated

- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementTypes.ts`
- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementImpressionStore.ts`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementsRoot.tsx`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementPopup.tsx`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementPopup.scss`
- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementCampaigns.ts`
- `jellyfin-web/src/apps/dashboard/routes/_asyncRoutes.ts`
- `jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx`

## Backend files added/updated

### New

- `MediaBrowser.Controller/FeatureAnnouncements/*`
- `Jellyfin.Api/Controllers/FeatureAnnouncementController.cs`
- `Jellyfin.Api/Models/FeatureAnnouncementDtos/*`
- `Jellyfin.Server.Implementations/FeatureAnnouncements/FeatureAnnouncementService.cs`
- `src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/FeatureAnnouncement.cs`
- `src/Jellyfin.Database/Jellyfin.Database.Implementations/Enums/FeatureAnnouncementStatus.cs`
- `src/Jellyfin.Database/Jellyfin.Database.Implementations/Enums/FeatureAnnouncementCtaTargetType.cs`
- `src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/FeatureAnnouncementConfiguration.cs`

### Updated

- `Jellyfin.Server/CoreAppHost.cs`
- `src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`

## Quick validation checklist

1. Open Dashboard and verify `Announcement` tab is visible for admins.
2. Open Announcement page and confirm seeded Leaderboard campaign is listed.
3. Use Preview and verify popup styling remains consistent.
4. Update text/media/CTA and Save.
5. Sign in with a regular user and verify active campaign uses updated values.
6. Verify per-session, per-day, and total impression limits behave correctly.
7. Create multiple published active campaigns and verify slide navigation appears.

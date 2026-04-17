# What's New / Feature Announcement System

## Purpose

This system provides a reusable post-login announcement framework for product updates (new features, improvements, seasonal campaigns, and onboarding reminders).

It is designed to avoid one-off hardcoded popups by using:
- a reusable campaign data model,
- a reusable popup UI component,
- reusable eligibility + impression tracking logic.

---

## What was implemented

### Web implementation files

- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementTypes.ts`
- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementCampaigns.ts`
- `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementImpressionStore.ts`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementPopup.tsx`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementPopup.scss`
- `jellyfin-web/src/components/featureAnnouncements/FeatureAnnouncementsRoot.tsx`

### App wiring

Mounted globally in both app layouts:
- `jellyfin-web/src/apps/stable/AppLayout.tsx`
- `jellyfin-web/src/apps/experimental/AppLayout.tsx`

This ensures eligible signed-in users can see active campaigns in normal post-login app flows.

---

## How it works

1. User signs in.
2. `FeatureAnnouncementsRoot` checks configured campaigns.
3. It finds the next eligible campaign based on:
   - `enabled`
   - active date window (`startsAt`, `endsAt`)
   - per-user impression count vs `maxImpressionsPerUser`
   - whether already shown in current sign-in session
4. When shown, an impression is recorded.
5. User can either:
   - click `Check It Out` (navigates to campaign route), or
   - click `Close`
6. Campaign continues to show on future sign-ins until impression cap is reached.

---

## Impression tracking model

### Persistent count (per user + campaign)

- Stored in `localStorage`
- Key format:
  - `jellyfin.featureAnnouncements.v1:<serverId>:<userId>`
- Value:
  - campaign map with `impressions`, `firstShownAt`, `lastShownAt`

### Session gate (per sign-in session)

- Stored in `sessionStorage`
- Key format:
  - `jellyfin.featureAnnouncements.session.v1:<serverId>:<userId>`
- Purpose:
  - prevent repeated popups for the same campaign within the same active session

### Current behavior notes

- Impression is counted when popup is opened.
- Therefore, both `Close` and `Check It Out` count for that display occurrence.
- For the same campaign, user sees at most once per session, and no more than configured max overall.

---

## Campaign configuration fields

Each campaign in `featureAnnouncementCampaigns.ts` supports:

- `id` (unique campaign/feature/update id)
- `enabled` (toggle campaign on/off)
- `startsAt`, `endsAt` (active window)
- `maxImpressionsPerUser` (display cap)
- `priority` (higher wins when multiple campaigns are eligible)
- `heading` (e.g., `What's New?`)
- `title`
- `subtitle`
- `description`
- `highlights` (2–4+ short bullets)
- `helpText` (quick instruction)
- `heroGifPath` (primary media/GIF)
- `mediaAssets` (optional additional screenshots/media)
- `ctaLabel`
- `ctaRoute`
- `closeLabel`

---

## Leaderboard campaign example (first use case)

Configured campaign id:
- `leaderboard-launch-2026`

Includes:
- `What's New?` heading
- Leaderboard-focused title/subtitle/description
- Highlights explaining ranking/progress comparison
- GIF hero + media asset using:
  - `jellyfin-web/src/assets/branding/request-popup-accent.gif`
- Primary CTA:
  - label: `Check It Out`
  - route: `/achievements`
- secondary action: `Close`
- max impressions:
  - `10` per user

---

## How to add a future update popup

1. Open `jellyfin-web/src/components/featureAnnouncements/featureAnnouncementCampaigns.ts`.
2. Add a new campaign object with a unique `id`.
3. Set `enabled`, `startsAt`, `endsAt`, and `maxImpressionsPerUser`.
4. Fill content fields (`title`, `description`, `highlights`, `helpText`).
5. Set media fields:
   - `heroGifPath`
   - optional `mediaAssets` screenshots
6. Set CTA:
   - `ctaLabel`
   - `ctaRoute`
7. Optional: raise/lower `priority` vs other campaigns.

No additional component wiring is needed.

---

## Enable / disable strategy

- To disable a campaign quickly: set `enabled: false`.
- To stop campaign naturally: set `endsAt` to a past date.
- To relaunch with fresh impression history:
  - create a new `id` (recommended), or
  - clear storage keys manually for testing environments.

---

## Technical assumptions and caveats

1. Persistence scope
   - Impression tracking currently uses browser storage (client-side).
   - It is per browser profile/device, not server-global across devices.

2. Why this fallback
   - No server persistence layer for announcement impressions was added in this iteration.
   - This is the cleanest low-friction reusable implementation with current architecture.

3. Future server-side option
   - If needed later, replace impression store functions with backend APIs while keeping:
     - campaign model,
     - popup UI,
     - root orchestration logic.

4. Route timing
   - Popup is intentionally not shown on auth/public flow paths such as `/login`, `/selectserver`, `/wizard`, etc.

---

## UX structure in popup

The reusable popup supports:
- Heading (`What's New?`)
- Feature title/subtitle
- GIF hero media area
- Additional screenshot/media gallery
- Short summary paragraph
- Bullet highlights
- Instruction/help text
- Primary CTA + secondary close action
- Responsive desktop/mobile layout

---

## Quick test checklist

1. Sign in as a user.
2. Verify popup appears for active eligible campaign.
3. Click `Close` and verify it does not reopen in same session.
4. Sign in again and verify impression increments.
5. Repeat until max impressions reached and verify popup stops auto-showing.
6. Click `Check It Out` and verify route opens `/achievements`.
7. Verify layout on desktop + narrow mobile viewport.

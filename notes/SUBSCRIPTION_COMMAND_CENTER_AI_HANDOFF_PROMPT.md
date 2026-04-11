# Subscription Command Center: Implementation Summary + AI Handoff Prompt

## 1. Feature Scope Implemented

The `Subscription Command Center` is implemented as an admin dashboard feature in Jellyfin with:

- Web route and drawer navigation entry
- Full React UI module with dark themed analytics cards and tools
- Live API integration against Jellyfin backend endpoints
- Backend admin endpoints that compute metrics from DB (no hardcoded mock dataset in production flow)
- Bulk key generation endpoint and frontend integration
- Mobile-specific header overlap/click fix for refresh button

Important: existing subscription lifecycle logic is not modified:

- `AccessKeyService`
- expiry date calculation flow
- `ExpiredSubscriptionMiddleware`
- `/Keys/Redeem`
- `/Keys/CurrentSubscription`

---

## 2. Frontend Implementation (jellyfin-web)

### Routing and navigation

- Route added in `jellyfin-web/src/apps/dashboard/routes/_asyncRoutes.ts`
  - `path: 'subscription-command-center'`
- Page route component:
  - `jellyfin-web/src/apps/dashboard/routes/subscription-command-center/index.tsx`
- Drawer entry added:
  - `jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx`
  - Label: `Subscription Command Center`

### Feature module location

`jellyfin-web/src/apps/dashboard/features/subscriptionCommandCenter`

Files:

- `index.tsx` (orchestrator/page shell)
- `data/api.ts` (API adapter + parsers + bulk-generate fallback)
- `utils/cn.ts`
- `components/OverviewCards.tsx`
- `components/ExpiryRadar.tsx`
- `components/KeyAnalytics.tsx`
- `components/BulkKeyGenerator.tsx`
- `components/CohortChart.tsx`
- `components/SystemHealth.tsx`
- `components/ResellerStats.tsx` (present but intentionally not rendered)
- `components/subscriptionCommandCenter.scss`

### Data flow

- `index.tsx` loads snapshot via `fetchDashboardSnapshot()`
- Snapshot includes:
  - `overview`
  - `radar`
  - `keyStats`
  - `cohorts`
  - `health`
- Expiring users loaded on demand via `fetchExpiringUsers(days)`
- Refresh re-runs snapshot load

### API adapter behavior

File: `data/api.ts`

- Uses `ServerConnections.currentApiClient().ajax(...)`
- Calls:
  - `GET Keys/AdminDashboard`
  - `GET Keys/AdminExpiringUsers?days={n}`
  - `POST Keys/GenerateBulk`
- Bulk generate fallback:
  - If `GenerateBulk` returns `404`, adapter falls back to repeated `POST Keys/Generate`
  - Fallback preserves UI metadata (`prefix`, `batchName`, `resellerTag`) client-side
- Parser supports both PascalCase and camelCase response properties

### UI behavior decisions currently active

- No internal subtab strip inside SCC page
- Reseller panel intentionally disabled in page render:
  - `ResellerStats.tsx` exists but is commented out in orchestrator
- Refresh error handling exists and displays inline message
- Skeleton states for loading
- INR formatting (`toLocaleString('en-IN')`) for revenue display

### Mobile fixes currently active

In `subscriptionCommandCenter.scss` and `index.tsx`:

- On mobile (`max-width: 900px`):
  - SCC sticky local top bar is hidden
  - Page header row (`title + refresh`) is used
  - SCC main content is pushed down to avoid collision with global fixed dashboard app bar:
    - `padding-top: 4.75rem`
    - root margin adjusted: `margin: 0 -0.75rem -0.75rem`
- Result:
  - Header no longer visually overlaps global avatar row
  - Refresh button is clickable on mobile

---

## 3. Backend Implementation (jellyfin/Jellyfin.Api)

### Controller updates

File: `jellyfin/Jellyfin.Api/Controllers/AccessKeyController.cs`

Added/implemented endpoints:

- `POST /Keys/GenerateBulk` (admin only)
- `GET /Keys/AdminDashboard` (admin only)
- `GET /Keys/AdminExpiringUsers?days={1..365}` (admin only)

### DTOs added

Folder: `jellyfin/Jellyfin.Api/Models/AccessKeyDtos`

- `AdminSubscriptionDashboardResponse.cs`
- `AdminSubscriptionOverviewResponse.cs`
- `AdminSubscriptionExpiryRadarResponse.cs`
- `AdminSubscriptionExpiringUserResponse.cs`
- `AdminSubscriptionKeyStatsResponse.cs`
- `AdminSubscriptionCohortResponse.cs`
- `AdminSubscriptionSystemHealthResponse.cs`
- `GenerateAccessKeysBulkRequest.cs`
- `GenerateAccessKeysBulkResponse.cs`

### Admin dashboard metric logic (DB-driven)

`GET /Keys/AdminDashboard`:

- Reads users and access keys from `JellyfinDbContext`
- Computes:
  - active, grace, expired users
  - expiring windows (24h, 3d, 7d, 30d)
  - total generated, redeemed, unused keys
  - expired redeemed key cycles
  - total revenue from `RedeemedAmount`
  - cohort series for last 7 months
  - rolling 30-day renewal rate
  - monthly growth from active user comparison

`GET /Keys/AdminExpiringUsers`:

- Filters users by expiry within selected horizon
- Gets each user latest redeemed key duration to map plan label
- Returns user rows: `UserId`, `Username`, `ExpiryDate`, `DaysRemaining`, `Plan`

`POST /Keys/GenerateBulk`:

- Validates quantity `1..1000`
- Loops `GenerateKey(durationMonths)` and returns `Items[]`

---

## 4. Known Constraints and Open Items

- Reseller logic is not wired to backend analytics yet, so Reseller section remains non-rendered.
- SCC is analytics/tooling layer; lifecycle business rules remain in existing services/middleware.
- Current frontend copy-to-clipboard uses `document.execCommand('copy')` fallback helper.
- Chart warnings about width/height can appear during initial layout on some mobile render timings.

---

## 5. Build and Deploy Notes

Web:

- `cd jellyfin-web`
- `npm run build:check`
- `npm run build:production`
- Deploy static files:
  - `robocopy "<repo>\\jellyfin-web\\dist" "<repo>\\_deploy\\server\\jellyfin-web" /MIR`

Runtime used for testing:

- server on port `8097`
- SCC route:
  - `/web/index.html#/dashboard/subscription-command-center`

---

## 6. Prompt For Future AI (Copy/Paste)

```text
You are working in Jellyfin repo at:
C:\\Users\\Barai Brothers\\Documents\\Jellyfin

Task: continue enhancing the existing "Subscription Command Center" feature without breaking existing subscription lifecycle logic.

Before coding:
1) Read these files first:
   - jellyfin-web/src/apps/dashboard/features/subscriptionCommandCenter/index.tsx
   - jellyfin-web/src/apps/dashboard/features/subscriptionCommandCenter/data/api.ts
   - jellyfin-web/src/apps/dashboard/features/subscriptionCommandCenter/components/subscriptionCommandCenter.scss
   - jellyfin/Jellyfin.Api/Controllers/AccessKeyController.cs
   - jellyfin/Jellyfin.Api/Models/AccessKeyDtos/*.cs
   - jellyfin-web/src/apps/dashboard/routes/subscription-command-center/index.tsx
   - jellyfin-web/src/apps/dashboard/routes/_asyncRoutes.ts
   - jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx

2) Respect non-negotiables:
   - Do NOT modify AccessKeyService behavior for existing lifecycle endpoints.
   - Do NOT break /Keys/Redeem or /Keys/CurrentSubscription behavior.
   - Keep SCC as analytics/tooling overlay.

3) Current known product decisions:
   - No sub-tabs inside SCC content.
   - Reseller panel currently disabled until backend logic is ready.
   - Data should come from DB-backed APIs, not copied mock data.
   - Mobile header must not overlap global app bar; refresh must stay clickable.

4) If adding new SCC metrics:
   - Prefer adding backend endpoint/DTO fields in AccessKeyController + AccessKeyDtos.
   - Keep frontend parser tolerant to PascalCase/camelCase.
   - Ensure mobile and desktop layouts both remain usable.

5) Validation expectations:
   - Build web: npm run build:check && npm run build:production
   - Verify SCC route loads and refresh button triggers GET /Keys/AdminDashboard
   - Verify mobile viewport (320x740 and 390x844) has no header overlap and refresh click works.

Deliverables:
   - concise summary of changes
   - exact file list touched
   - test evidence (build + runtime checks)
```


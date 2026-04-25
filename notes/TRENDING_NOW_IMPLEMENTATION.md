# Trending Now 3-Layer OTT Homepage System

## Goal

Implement a single homepage `Trending Now` rail for KnightFlix that behaves like a modern OTT hero showcase while still fitting the existing Jellyfin and KnightFlix UI framework.

This version replaces the earlier lightweight period-filter row with one featured homepage experience powered by:

1. `Base Trending Layer`
2. `Personalization Layer`
3. `Admin Promotion Layer`

## What Was Implemented

### Backend services

The Trending pipeline is now split into focused services:

- `TrendingBaseService`
  - computes the neutral candidate set from validated watch sessions
- `TrendingPersonalizationService`
  - calculates viewer affinity and audience matching from existing KnightFlix behavior data
- `TrendingPromotionService`
  - stores and resolves active admin/editorial promotions
- `TrendingNowService`
  - merges all three layers into one final ranked homepage rail

Primary backend files:

- `jellyfin/Jellyfin.Server.Implementations/Tracking/TrendingBaseService.cs`
- `jellyfin/Jellyfin.Server.Implementations/Tracking/TrendingPersonalizationService.cs`
- `jellyfin/Jellyfin.Server.Implementations/Tracking/TrendingPromotionService.cs`
- `jellyfin/Jellyfin.Server.Implementations/Tracking/TrendingNowService.cs`
- `jellyfin/MediaBrowser.Controller/Trending/*`
- `jellyfin/Jellyfin.Api/Controllers/TrendingController.cs`
- `jellyfin/Jellyfin.Api/Models/ActivityDtos/*`

### Admin promotion storage

Added a reusable promotion model for the admin/editorial layer:

- `TrendingPromotions` table
- entity: `TrendingPromotion`
- EF config: `TrendingPromotionConfiguration`

Primary persistence files:

- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/TrendingPromotion.cs`
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/TrendingPromotionConfiguration.cs`
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`

### Frontend homepage experience

The homepage Trending feature is now a single featured OTT rail instead of a plain card row.

It includes:

- featured hero slide
- backdrop-driven wallpaper treatment
- poster artwork
- title and overview
- explanation badges
- `Play` and `More Info` actions
- auto-rotation
- transparent left/right navigation arrows with wrap-around navigation
- rotating marketing subtitle copy (changes on each homepage entry/refresh)

Primary frontend files:

- `jellyfin-web/src/components/homesections/sections/trendingNow.ts`
- `jellyfin-web/src/components/homesections/sections/trendingNow.scss`
- `jellyfin-web/src/utils/trendingNowApi.ts`
- `jellyfin-web/src/components/homesections/homesections.js`

### Admin dashboard management

Added a dedicated admin page for the promotion layer:

- dashboard route: `/dashboard/trending`
- list + form management flow
- create/update/delete
- enable/disable
- preview of effective badge and audience treatment

Primary admin UI files:

- `jellyfin-web/src/apps/dashboard/features/trending/index.tsx`
- `jellyfin-web/src/apps/dashboard/features/trending/trending.scss`
- `jellyfin-web/src/apps/dashboard/routes/trending/index.tsx`
- `jellyfin-web/src/utils/trendingPromotionsApi.ts`
- `jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx`
- `jellyfin-web/src/apps/dashboard/routes/_asyncRoutes.ts`

### Admin UX clarity updates (latest)

The promotion form was refined to reduce admin confusion and prevent layout collisions:

- `Item ID` is no longer a manual entry flow in the form UI.
  - Admin now selects `Content [Movie/Series]` via a searchable picker.
  - The selected content id is still stored/sent in the background as `itemId`.
- `Promotion ID` now has predefined suggestion patterns and manual override support.
  - Added a `Promotion Pattern` dropdown for quick naming standards.
  - Added Promotion ID suggestion options so admins can choose consistent IDs without memorizing formats.
- Added tooltip guidance for all key form fields so admins understand purpose and expected values directly in-context:
  - Promotion Pattern, Promotion ID, Content, Enabled
  - Audience Segment/Value
  - Pin Position, Boost Amount
  - Start/End UTC
  - Label/Tagline override
  - Artwork Variant
- Layout hardening to avoid overlap:
  - Important long fields now span full width where needed.
  - Earlier responsive collapse for form columns on narrower widths to prevent squeezed two-column collisions.

### Rail size control (new)

Added admin control for homepage Trending rail size:

- New setting: `Max Slides` (range `1-30`, default `12`)
- Admin can update it from Trending dashboard header controls
- Homepage now reads this setting and requests exactly that many Trending items
- If settings API fails, homepage safely falls back to default `12`

API support:

- `GET /api/activity/trending-now/settings`
- `POST /api/activity/trending-now/settings` (admin/elevated)

## 3-Layer Ranking Model

### Layer 1: Base Trending

This layer answers:

`What is objectively trending across KnightFlix right now?`

Data source:

- `UserWatchSessions`
- only validated sessions
- only rows with `ValidatedTicks > 0`
- only browseable video targets

Normalization:

- movies trend as themselves
- episode sessions roll up to the parent series when available

Base score formula:

```text
base_score =
  (watchHours * 5.5)
  + (uniqueViewers * 16)
  + (starts * 4)
  + (completions * 12)
  + (momentumWatchHours * 7)
  + (completionRate * 10)
  + (freshnessRatio * 8)
```

Momentum windows:

- week: trailing 2 days
- month: trailing 7 days
- season: trailing 30 days

### Layer 2: Personalization

This layer answers:

`Which of the trending candidates are most relevant for this viewer?`

Signals reused from existing KnightFlix behavior systems:

- top genres from period stats
- movie-heavy vs series-heavy preference
- completion tendency
- continue-watching volume
- binge behavior
- recent viewing distribution

Important rule:

Personalization does not create a separate recommendation rail. It only reorders the base candidate pool.

Personalization boost behavior:

- strong genre match gives the biggest boost
- movie-heavy users can boost movies
- series-heavy users can boost series
- binge and completion behavior add smaller soft boosts

Personalization cap:

- boost is capped at `35%` of the item base score
- low-history users fall back toward base trending order

### Layer 3: Admin Promotion

This layer answers:

`What content should KnightFlix intentionally push on the homepage right now?`

Supported controls:

- pin position
- additive boost
- start / end schedule
- enabled state
- audience segment targeting
- label override
- tagline override
- artwork variant hint

Audience segments in v1:

- `AllUsers`
- `NewOrLowHistory`
- `ReturningUsers`
- `MovieHeavy`
- `SeriesHeavy`
- `TopGenreMatch`

## Final Ranking Flow

Final score:

```text
final_score = base_score + personalization_boost + admin_boost
```

Ordering rules:

1. active pinned promotions by `pin_position`
2. everything else by `final_score`
3. tie-break by:
   - `base_score`
   - `momentum`
   - `unique_viewers`
   - `title`

Promotion application rules:

- inactive promotions are ignored
- out-of-window promotions are ignored
- audience-mismatched promotions are ignored
- pin beats normal score ordering
- admin label override wins over algorithmic label

## Homepage Behavior

### Current homepage scope

Homepage UI uses one main OTT-style Trending rail.

Visible homepage window for v1:

- `This Week`

The backend still supports `week`, `month`, and `season`, but the homepage hero focuses on current weekly trending so the experience stays premium and simple.

### Featured slide contents

Each active slide can show:

- backdrop image
- poster image
- title
- compact metadata line
- overview
- primary badge
- optional secondary badge
- explanation text
- CTA buttons

### Auto-rotation

The hero rail auto-rotates through the ranked items.

Behavior:

- rotates every few seconds
- does not show manual thumbnail-strip controls
- transparent left and right arrows move previous/next with infinite loop behavior
- no page reload

### CTA behavior

- `Play` uses the existing playback flow
- `More Info` routes into the existing item detail page

## Badge and Explanation Strategy

Primary label precedence:

1. admin `label_override`
2. admin default like `Featured` or `Editor's Pick`
3. personalization default `Recommended for You`
4. base trending labels like:
   - `#Trending #1`
   - `Hot #2`
   - `Hot #3`
   - `Trending Now`

Secondary labels:

- `#1 This Week`
- `Trending in Action`
- empty when extra badge noise is unnecessary

Explanation text examples:

- `Featured by KnightFlix`
- `Because you watch Action`
- `Featured for movie fans`
- `Popular this week`

## Metadata and Artwork Reuse

The Trending rail reuses existing Jellyfin library metadata and artwork already downloaded during normal content ingestion.

Assets and metadata reused:

- backdrop images
- poster images
- title
- overview
- genres
- production year
- runtime
- rating

No separate manual content entry system was added for the homepage rail.

## API Surface

### User-facing endpoint

- `GET /api/activity/trending-now?period=week|month|season&limit=1-30`

Response now includes:

- item identity
- base score
- personalization boost
- admin boost
- final score
- rank
- labels
- explanation source
- overview
- genres
- year
- runtime
- rating
- image availability hints

### Admin endpoints

- `GET /api/activity/trending-now/promotions`
- `POST /api/activity/trending-now/promotions/upsert`
- `POST /api/activity/trending-now/promotions/{id}/enabled`
- `DELETE /api/activity/trending-now/promotions/{id}`

## Fallback Logic and Edge Cases

### New or low-history user

- personalization is reduced or skipped
- rail relies mostly on base trending plus any matching admin promotions

### No active admin promotions

- rail runs on base trending + personalization only

### Sparse server activity

- fallback mode is flagged in the API
- ranking stays usable with a smaller candidate pool

### Admin-only item with no organic trend score

- active promotion can force inclusion into the candidate set
- item still uses normal metadata and artwork hydration

## UI Alignment Notes

The final implementation was intentionally kept inside existing KnightFlix and Jellyfin conventions:

- homepage section integration stayed inside the existing `homesections` framework
- dashboard management followed the existing Announcement-style admin pattern
- dark theme, border treatment, spacing, and typography stayed within current KnightFlix surfaces
- mobile layout keeps the same responsive behavior already used across KnightFlix custom pages
- item navigation uses the existing Jellyfin detail route and playback system

This keeps the feature feeling like a natural KnightFlix extension instead of an embedded external widget.

## Verification

Backend verification:

- `dotnet test jellyfin/tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj -c Release --filter FullyQualifiedName~TrendingControllerTests`
- result: passed (`8/8`)

Frontend verification:

- `npm run build:check` in `jellyfin-web`
- result: passed

## Local Mock Data Test (8097)

Added reusable seed script:

- `notes/TRENDING_NOW_MOCK_DATA_8097.sql`

What it seeds:

- 9 valid `UserWatchSessions` in the current week window
- 1 active `TrendingPromotions` row (`mock-trending-featured-border2`)
- deterministic coverage for:
  - base trending
  - episode-to-series rollup
  - admin pin + boost + label override

Run seed:

- `sqlite3 "C:\Users\Barai Brothers\Documents\Jellyfin\.run\jf-8097\data\data\jellyfin.db" ".read 'C:/Users/Barai Brothers/Documents/Jellyfin/notes/TRENDING_NOW_MOCK_DATA_8097.sql'"`

Verify API quickly:

- authenticate:
  - `POST http://localhost:8097/Users/AuthenticateByName`
- fetch:
  - `GET http://localhost:8097/api/activity/trending-now?period=week&limit=5`

Expected after seed:

- `Border 2` appears with admin promotion (`Featured`, pin position `1`)
- `The Fragrant Flower Blooms with Dignity` appears from rolled-up episode engagement
- response includes non-zero `BaseScore`, `AdminBoost`, `FinalScore`, and explanation labels

## Follow-Up Opportunities

- Add richer item-picker UX in the dashboard so admins do not need to paste raw item ids
- Add service-level ranking tests for pinning and personalization edge cases
- Add browser automation coverage for homepage hero rotation and admin save flows
- Expose month and season hero modes later if product wants a larger Trending program

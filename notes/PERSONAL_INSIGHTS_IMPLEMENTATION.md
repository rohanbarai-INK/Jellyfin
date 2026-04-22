# Personal Insights Implementation

**Date**: March-April 2026
**Commit**: `f0416c31829fdf5fb52c37e938c818ec90c2b8de`
**Context**: Added a personal analytics dashboard for users (watch time, peak hours, binge streaks, and genre preferences) with backend aggregation and a new frontend page.

## April 2026 Follow-Up Fixes (Data Consistency + Peak Hours + Streak Reliability + Genre Signal Quality)

Observed production issue:
- The summary could show low `Total Watch Time` while also showing high `Movies Watched` / `Episodes Watched`.
- Peak viewing could present a confident "You are most active around X" even when the selected period had no activity.

Root cause:
- `Total Watch Time` and peak-hour charts are computed from validated watch sessions (aggregated insight tables).
- `Movies Watched` / `Episodes Watched` were previously counted from `UserData.Played` (a different source of truth).

Fix applied:
- Summary completion counts now come from the same aggregated source as watch time: `UserPeriodStats.CompletedMovies` and `UserPeriodStats.CompletedEpisodes`.
- Peak viewing includes `HasViewingActivity` so the UI can avoid claiming a peak hour when there is no activity for the selected period.
- Tracking recovers plausible validated ticks on final stop events when clients report sparse progress and then send a large final position jump.
- Binge detection accepts both `Episode` and fully-qualified episode type names.
- Genre aggregation for episodes now falls back to parent series genres when episode genre metadata is missing.
- Smart Insight avoids low-signal statements like `0% <genre>` and switches to a neutral "exploring multiple genres" message when top-genre share is below 1%.

## April 22, 2026 Follow-Up Fix (Episode Completion Accuracy from Playback Stop Signal)

Observed production issue:
- Personal Insights could show high `Total Watch Time` but low `Episodes Watched` for users who had clearly finished multiple episodes.

Root cause:
- Aggregation used a hardcoded completion rule (`validatedTicks >= 90% runtime`) to increment `CompletedEpisodes`.
- In real playback flows, anti-abuse validation may undercount validated ticks even when Jellyfin itself already marks playback as completed at stop time.
- This made completion counters stricter than Jellyfin's own `PlayedToCompletion` outcome.

Fix applied:
- `WatchSessionTrackingService` now forwards `PlaybackStopEventArgs.PlayedToCompletion` to aggregation.
- `WatchSessionAggregationService` now treats a session as completed when either:
  - playback stop reports `PlayedToCompletion = true`, or
  - validated ticks still satisfy the fallback runtime threshold.
- This keeps watch-time anti-abuse validation intact while aligning completion counts with authoritative stop-time completion state.

Validation:
- Added regression test:
  - `PlaybackStopPlayedToCompletion_MarksEpisodeCompletedForInsights`
- The test covers an episode session below 90% validated ticks that is still marked completed by stop signal and now correctly increments `CompletedEpisodes`.

---

## Overview

Personal Insights is a server-authoritative analytics feature that:
- Tracks playback activity as validated watch sessions (anti-abuse aware)
- Aggregates sessions into hourly/period/genre/binge tables
- Exposes a single API endpoint for user insights
- Renders a full dashboard UI in the web app

The data flow is:

```
Playback Events
  -> PlaybackStart/Progress/Stop consumers
      -> WatchSessionTrackingService (validate + persist session)
          -> WatchSessionAggregationService (period/hour/genre/binge)
              -> PersonalInsightsService (read models)
                  -> GET /api/activity/personal-insights
                      -> personalinsights.tsx UI
```

---

## Backend Implementation (C# / .NET)

### 1) Eventing Consumers
Files:
- `jellyfin/Jellyfin.Server.Implementations/Events/Consumers/Session/PlaybackStartTracker.cs`
- `jellyfin/Jellyfin.Server.Implementations/Events/Consumers/Session/PlaybackProgressTracker.cs`
- `jellyfin/Jellyfin.Server.Implementations/Events/Consumers/Session/PlaybackStopTracker.cs`
- `jellyfin/Jellyfin.Server.Implementations/Events/EventingServiceCollectionExtensions.cs`

These consumers hook into `PlaybackStart`, `PlaybackProgress`, and `PlaybackStop` events and forward them to the tracking service. Registration is done in `AddEventServices()`.

### 2) Watch Session Tracking (Anti-abuse layer)
File: `jellyfin/Jellyfin.Server.Implementations/Tracking/WatchSessionTrackingService.cs`

Key behaviors:
- Maintains in-flight sessions in memory using a `ConcurrentDictionary` keyed by `userId:itemId:sessionId`.
- Ignores automated progress events (`eventArgs.IsAutomated`) to avoid false deltas.
- Validates progress deltas with guardrails (speed, seeks, jitter, max duration).
- Uses a `SuspicionScore` with threshold `5`; exceeding it marks a session invalid.
- Applies bounded validated-tick recovery on final stop events to reduce undercounting from sparse client reporting.

Finalized sessions are persisted to `UserWatchSession` and immediately sent for aggregation.

### 3) Aggregation (Insight tables)
File: `jellyfin/Jellyfin.Server.Implementations/Tracking/WatchSessionAggregationService.cs`

Aggregates validated sessions into period-based tables:
- Period types: Month, Year, AllTime (`PeriodType` enum)
- Period keys:
  - Month: `yyyy-MM`
  - Year: `yyyy`
  - All time: `ALL`

What it computes per session:
- Period totals (`UserPeriodStats`): `TotalValidatedTicks`, `SessionCount`, `CompletedMovies`, `CompletedEpisodes`, `BingeSessions`
- Hourly distribution (`UserPeriodHourlyStats`): validated ticks per hour bucket (24 buckets)
- Genre totals (`UserGenrePeriodStats`): validated ticks by genre
- Binge sessions (`UserBingeSession`): detects 3+ consecutive episodes

Other notes:
- Completion threshold: 90% of runtime
- Hour bucketing uses the Insights time zone (prefers `Asia/Kolkata`, fallback `India Standard Time`, else UTC)
- Binge episode matching supports both simple and fully-qualified episode type names from `BaseItems.Type`.
- For episode sessions with missing episode genres, aggregation uses series genres so genre insights remain meaningful.

### 4) Read Models
File: `jellyfin/Jellyfin.Server.Implementations/Tracking/PersonalInsightsService.cs`

Builds the final response by combining:
- Current + previous period stats (for deltas)
- Hourly distribution (24 points)
- Top genres (top 3)
- Binge history (recent 3)
- Continue-watching items (top 5 from `UserData`)
- Generated insight text (based on top genre)

Summary logic:
- `TotalWatchHours = TotalValidatedTicks / TimeSpan.TicksPerHour`
- `WatchTimeChangePercent` compares current vs previous period ticks
- `MoviesWatched` and `EpisodesWatched` come from aggregated `UserPeriodStats.CompletedMovies/CompletedEpisodes`
- `EngagementPercentile`: 0-99 derived from watch-time delta
- If top-genre share is below 1%, Smart Insight returns a neutral exploration message instead of rounding to `0%`.

Peak viewing logic:
- `HourlyDistribution` is sourced from `UserPeriodHourlyStats` for the selected period.
- `HasViewingActivity = any(HourlyDistribution.Minutes > 0)`.
- If `HasViewingActivity` is false, the service returns a neutral label (`"No activity yet"`) and does not pretend there is a meaningful peak hour.

### 5) API Endpoint
File: `jellyfin/Jellyfin.Api/Controllers/PersonalInsightsController.cs`

Route:
```
GET /api/activity/personal-insights?period=month|year|all&userId=<optional>
```

Rules:
- Default period: `month`
- `userId` is optional, but only admins can request another user's insights

DTO response:
- `PersonalInsightsResponseDto`
- `PersonalInsightsSummaryDto`
- `PersonalInsightsPeakViewingDto` (includes `HasViewingActivity`)
- `PersonalInsightsContinueWatchingDto`
- `PersonalInsightsBingeDto` + `PersonalInsightsRecentBingeDto`
- `PersonalInsightsGenreDto`

### 6) Data Model (Entities)
Key entities:
- `UserWatchSession`
- `UserPeriodStats`
- `UserPeriodHourlyStats`
- `UserGenrePeriodStats`
- `UserBingeSession`

### 7) Dependency Injection
File: `jellyfin/Jellyfin.Server/CoreAppHost.cs`

Registered in DI:
- `WatchSessionTrackingService`
- `WatchSessionAggregationService`
- `IPersonalInsightsService` -> `PersonalInsightsService`
- `TimeProvider.System`

---

## Frontend Implementation (React/TypeScript)

### 1) Route & Entry Points
Files:
- `jellyfin-web/src/apps/stable/routes/user/personalinsights.tsx`
- `jellyfin-web/src/apps/stable/routes/asyncRoutes/user.ts`

The page lives at:
```
#/personalinsights?period=month&userId=<id>
```

### 2) UI Page
File: `jellyfin-web/src/apps/stable/routes/user/personalinsights.tsx`

Key UI features:
- Period selector (Month / Year / AllTime)
- Summary cards (watch hours, movies, episodes, engagement score)
- Hourly histogram + tooltip
- Continue Watching list
- Binge highlights + recent streaks
- Genre donut chart + hover detail
- Smart Insight text block

Peak viewing UI notes:
- Uses `peakViewing.hasViewingActivity` to decide whether to show peak-hour messaging.
- Avoids highlighting a peak bar when there is no activity for the selected period.

### 3) Styles
File: `jellyfin-web/src/styles/personalinsights.scss`

---

## Testing

Files:
- `jellyfin/tests/Jellyfin.Api.Tests/Controllers/PersonalInsightsControllerTests.cs`
- `jellyfin/tests/Jellyfin.Server.Implementations.Tests/Tracking/WatchSessionTrackingAndAggregationTests.cs`

Coverage includes:
- API period parsing and authorization
- Watch session tracking correctness
- Aggregation into period/hour/genre/binge tables
- Insight payload integrity
- Completion counts are sourced from aggregated stats (not `UserData`)
- "No activity" periods do not claim peak viewing
- Sparse final-stop sessions recover realistic validated watch time.
- Binge detection works for production-style fully-qualified episode type values.
- Genre aggregation uses series fallback when episode genre metadata is absent.

---

## Notes

- Hour bucketing uses `Asia/Kolkata` when available (Indian user baseline).
- Continue watching uses `UserData` to pull latest incomplete items.
- Genre stats use top 3 genres by validated watch ticks.
- Binge requires 3+ consecutive episodes in a series.

Period filter behavior:
- The period filter (`month`, `year`, `all`) applies to the entire selected period, not "today".
- If you haven't watched anything today but watched earlier in the period, Peak Viewing Hours and watch time can still be non-zero.

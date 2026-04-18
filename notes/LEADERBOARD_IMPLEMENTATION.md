# Seasonal Leaderboard System Implementation

**Date**: April 2026  
**Context**: Integrated a seasonal leaderboard as a dedicated user page to track user XP, levels, and rankings per season.

---

## Overview

The leaderboard is now a metric-aware seasonal competition system available as a dedicated page (`#/leaderboard`) with direct navigation links from profile menus.

Current behavior includes:
- Multi-metric ranking tabs: `xp`, `watchtime`, `movies`, `series`, `genres`, `streak`, `achievements`
- Rich leaderboard rows with user identity (name/avatar), badges, and primary/secondary metric labels
- Offset-based pagination with inline `...` continuation and `Load more`
- Personal competition context (next target / your rank / behind you) rendered in the same leaderboard flow
- Conditional Top 10 UX so users inside Top 10 see inline competition context directly under Top 10 rows without duplicated lower-ranking competition blocks

---

## Backend Implementation (C# / .NET)

### 1. Entity: `UserSeasonStats`

**File**: `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/UserSeasonStats.cs`

Caches per-user seasonal stats used to rank all leaderboard metric tabs.

```csharp
public class UserSeasonStats
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int SeasonYear { get; set; }
    public long TotalXp { get; set; }
    public long AchievementXp { get; set; }
    public int AchievementCount { get; set; }
    public int Level { get; set; }
    public long TotalWatchMinutes { get; set; }
    public int MoviesCompleted { get; set; }
    public int SeriesCompleted { get; set; }
    public int UniqueGenresWatched { get; set; }
    public int CurrentStreakDays { get; set; }
    public int BestStreakDays { get; set; }
    public int AchievementsUnlocked { get; set; }
    public int ApprovedRequests { get; set; }
    public DateTime LastUpdatedUtc { get; set; }

    public User User { get; set; }
}
```

### 2. Model Configuration

**File**: `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/UserSeasonStatsConfiguration.cs`

Defines indexes for efficient metric-specific ranking:
- Unique index on `(UserId, SeasonYear)`
- Ranking indexes by season for XP, watch time, movies, series, genres, streak, achievements, and requests

### 3. DbContext Update

**File**: `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`

Added `DbSet<UserSeasonStats>` to expose the entity in EF Core.

```csharp
public DbSet<UserSeasonStats> UserSeasonStats => Set<UserSeasonStats>();
```

### 4. EF Core Migration

**File**: `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260304100000_AddUserSeasonStats.cs`

Manual migration to create the `UserSeasonStats` table with foreign key to `Users` and the two indexes.

### 5. Model Snapshot Update

**File**: `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/JellyfinDbModelSnapshot.cs`

Added the `UserSeasonStats` entity definition and its relationship to the EF Core model snapshot so migrations can detect it.

### 6. Interface: `ILeaderboardService`

**File**: `jellyfin/MediaBrowser.Controller/Leaderboard/ILeaderboardService.cs`

```csharp
public interface ILeaderboardService
{
    Task<LeaderboardPersonalInfo> GetPersonalStats(Guid userId, int seasonYear, string metricType = "xp");
    Task<LeaderboardTopResult> GetTopLeaderboard(Guid requestingUserId, int seasonYear, int limit, string metricType = "xp", int offset = 0);
    Task RecordAchievementXp(Guid userId, int seasonYear, int xp, int coins);
}
```

### 7. Info Types

**Files**:
- `jellyfin/MediaBrowser.Controller/Leaderboard/LeaderboardEntryInfo.cs`
- `jellyfin/MediaBrowser.Controller/Leaderboard/LeaderboardCompetitionInfo.cs`
- `jellyfin/MediaBrowser.Controller/Leaderboard/LeaderboardPersonalInfo.cs`
- `jellyfin/MediaBrowser.Controller/Leaderboard/LeaderboardTopResult.cs`

Contracts now include:
- Rich entry identity and presentation fields (`UserName`, `PrimaryImageTag`, `BadgeKind`, `MetricLabel`, `SecondaryMetricLabel`)
- Explicit competition neighbors (`NextTarget`, `BehindUser`) and `CurrentUserEntry`
- Metric type and pagination metadata (`Offset`, `Limit`, `HasMore`)

### 8. Service Implementation

**File**: `jellyfin/Jellyfin.Server.Implementations/Leaderboard/LeaderboardService.cs`

Key methods:
- `GetPersonalStats` — metric-aware personal standings + percentile + immediate competition neighbors
- `GetTopLeaderboard` — metric-aware ranked slices with `offset`/`limit` pagination
- `GetSeasonRowsAsync` — cached ordered season rows used by both personal and top queries
- `BuildEntry` / `BuildCompetitionInfo` — shared mapping logic for rich entries and neighbor context
- `RecordAchievementXp`, `RecordPlaybackStats`, `RecordApprovedRequest` — update seasonal stats incrementally
- `EnsureTableExistsAsync` — defensive table/index creation for local environments missing migrations

### 9. Controller

**File**: `jellyfin/Jellyfin.Api/Controllers/LeaderboardController.cs`

- `GET /Leaderboard/Me?seasonYear=2026&type=xp` — metric-aware personal stats
- `GET /Leaderboard/Top?seasonYear=2026&type=xp&limit=10&offset=0` — metric-aware paged top entries

### 10. DTOs

**Folder**: `jellyfin/Jellyfin.Api/Models/LeaderboardDtos/`

- `LeaderboardPersonalResponse` — includes metric type/label, current row, next target, behind user
- `LeaderboardEntryDto` — includes identity/avatar/badge and primary/secondary metric labels
- `LeaderboardCompetitionDto` — explicit next/behind competition row contract
- `LeaderboardTopResponse` — includes metric type + `offset`/`limit`/`hasMore`

### 11. Dependency Injection

**File**: `jellyfin/Jellyfin.Server/CoreAppHost.cs`

Registered `LeaderboardService` as a singleton in DI container.

```csharp
services.AddSingleton<ILeaderboardService, LeaderboardService>();
```

### 12. XP Hook

**File**: `jellyfin/Jellyfin.Server.Implementations/Achievements/AchievementService.cs`

- In `Unlock` method: calls `ILeaderboardService.RecordAchievementXp()` after successful unlock
- In `Sync` method: calls `ILeaderboardService.RecordAchievementXp()` after batch save
- Wrapped in `try/catch` to avoid breaking achievement flow if leaderboard fails

---

## Frontend Implementation (React/TypeScript)

### 1. API Adapter

**File**: `jellyfin-web/src/utils/leaderboardApi.ts`

Typed API calls with PascalCase → camelCase normalization:
- `getLeaderboardPersonal(seasonYear, metricType)` → `/Leaderboard/Me?type=...`
- `getLeaderboardTop(seasonYear, limit, metricType, apiClient, offset)` → `/Leaderboard/Top?type=...&offset=...`
- DTO parsing for rich rows, competition neighbors, and pagination metadata

### 2. Leaderboard Component

**File**: `jellyfin-web/src/apps/stable/routes/user/LeaderboardTab.tsx`

The leaderboard UI is implemented as one continuous leaderboard experience:
- Metric tab strip for all supported leaderboard metrics
- Unified leaderboard board/table with rich rows
- Inline continuation row (`...`) and paginated load more behavior
- Same-table personal competition context (next target / your rank / behind you)

### Top 10 Conditional Improvement (April 2026)

Implemented conditional render logic to avoid duplicated competition UX for users already inside Top 10.

Behavior:
- **Rank > 10**:
  1. Show top rows
  2. Show continuation/load-more rows as needed
  3. Show competition rows in the same table area below
- **Rank <= 10**:
  1. Show Top 10 rows
  2. Render compact inline competition strip directly under Top 10 rows in the same board
  3. Suppress the duplicated lower-rank competition row block

Edge handling:
- Rank 1: no broken “next target” row; leader messaging shown instead
- Rank 10: still shows rank 11 as “behind you” when available via backend-provided neighbor data
- Metric-aware gap text remains driven by backend `GapLabel` / `MetricLabel` values

### 3. Dedicated Leaderboard Route

**Files**:
- `jellyfin-web/src/apps/stable/routes/user/leaderboard.tsx`
- `jellyfin-web/src/apps/stable/routes/asyncRoutes/user.ts`
- `jellyfin-web/src/apps/experimental/routes/asyncRoutes/user.ts`

Leaderboard is now its own page at:
- `#/leaderboard?userId=<id>`

`LeaderboardTab.tsx` is reused inside this dedicated route to keep the same leaderboard UI and behavior.

Achievements remains a separate page (`#/achievements`) focused only on achievement history/progression.

### 4. Profile Navigation Entry

**Files**:
- `jellyfin-web/src/components/toolbar/AppUserMenu.tsx`
- `jellyfin-web/src/apps/stable/routes/user/settings/index.tsx`

Added a direct **Leaderboard** option in profile-related menus, alongside existing Request/Achievements-style quick links, so users can open leaderboard directly without entering Achievements first.

### 5. Styles

**File**: `jellyfin-web/src/apps/stable/routes/user/leaderboard.scss`

Dark-theme, responsive styles (mobile breakpoint at 640px). Leaderboard styles are scoped for both `#achievementsPage` and `#leaderboardPage` to support shared leaderboard UI rendering.

Recent additions:
- Inline Top 10 competition strip card styling
- Visual modifiers for current-user/leader/empty competition cards
- Mobile-friendly single-column competition strip layout

---

## Bug Fix: Missing Table Error

### Problem
When testing the leaderboard tab, backend endpoints returned `500 Internal Server Error`:
```
SQLite Error 1: 'no such table: UserSeasonStats'
```

The EF Core migration wasn’t applied in the local dev environment, so queries failed.

### Solution
Modified `LeaderboardService` to auto-create the table if missing:

```csharp
private async Task EnsureTableExistsAsync(JellyfinDbContext dbContext)
{
    if (_tableVerified) return;

    const string Sql = @"
        CREATE TABLE IF NOT EXISTS UserSeasonStats (
            Id TEXT NOT NULL PRIMARY KEY,
            UserId TEXT NOT NULL,
            SeasonYear INTEGER NOT NULL,
            TotalXp INTEGER NOT NULL DEFAULT 0,
            AchievementXp INTEGER NOT NULL DEFAULT 0,
            AchievementCount INTEGER NOT NULL DEFAULT 0,
            Level INTEGER NOT NULL DEFAULT 0,
            LastUpdatedUtc TEXT NOT NULL,
            CONSTRAINT FK_UserSeasonStats_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_UserSeasonStats_UserId_SeasonYear ON UserSeasonStats (UserId, SeasonYear);
        CREATE INDEX IF NOT EXISTS IX_UserSeasonStats_SeasonYear_TotalXp ON UserSeasonStats (SeasonYear, TotalXp);
    ";

    await dbContext.Database.ExecuteSqlRawAsync(Sql).ConfigureAwait(false);
    _tableVerified = true;
}
```

Added calls to `EnsureTableExistsAsync()` in:
- `GetOrCreateSeasonStats`
- `GetTopLeaderboard`
- `RecordAchievementXp`

### Verification
- Rebuilt and redeployed local dev server
- API calls now return `200`:
  - `GET /Leaderboard/Me` → returns personal stats
  - `GET /Leaderboard/Top?limit=5` → returns top entries

---

## Bug Fix: Missing Users in Leaderboard

### Problem
Leaderboard showed only users who had logged in after feature implementation. Existing users who never triggered leaderboard-related activity (playback, achievements, requests) were missing from `UserSeasonStats` table and thus excluded from rankings.

Example: local environment had 6 users but leaderboard showed only 3 (Test, baraibrothers, Rohan).

### Root Cause
`UserSeasonStats` rows were created lazily per user only when:
- User triggered playback (`RecordPlaybackStats`)
- User unlocked achievements (`RecordAchievementXp`)
- User had approved requests (`RecordApprovedRequest`)

Leaderboard read operations (`GetTopLeaderboard`, `GetPersonalStats`) only queried `UserSeasonStats`, so users without rows never appeared.

### Solution
Added backfill logic in `LeaderboardService.cs` to ensure all current users have a season row before leaderboard reads:

```csharp
private async Task EnsureSeasonRowsForAllUsersAsync(JellyfinDbContext dbContext, int seasonYear)
{
    var allUserIds = _userManager.Users
        .Select(user => user.Id)
        .ToArray();

    if (allUserIds.Length == 0)
    {
        return;
    }

    var existingUserIds = await dbContext.UserSeasonStats
        .AsNoTracking()
        .Where(stats => stats.SeasonYear == seasonYear)
        .Select(stats => stats.UserId)
        .ToListAsync()
        .ConfigureAwait(false);

    var existingSet = new HashSet<Guid>(existingUserIds);
    foreach (var userId in allUserIds)
    {
        if (existingSet.Contains(userId))
        {
            continue;
        }

        _ = await GetOrCreateSeasonStats(dbContext, userId, seasonYear).ConfigureAwait(false);
    }
}
```

Called from `GetSeasonRowsAsync` before querying leaderboard rows:
```csharp
await EnsureTableExistsAsync(dbContext).ConfigureAwait(false);
await EnsureSeasonRowsForAllUsersAsync(dbContext, seasonYear).ConfigureAwait(false);
```

### Behavior After Fix
- All existing users are backfilled with a `UserSeasonStats` row for the current season on first leaderboard read
- Users with no activity appear with zero metrics (0 XP, 0 watch time, etc.)
- Total users count in leaderboard reflects actual user count in system
- Backfill runs once per season year; subsequent reads use cached rows

### Verification
- Rebuilt backend: `dotnet build Jellyfin.Server.Implementations/Jellyfin.Server.Implementations.csproj`
- Build succeeded (existing package vulnerability warning unrelated to this change)

---

## Deployment Notes

### Local Dev Server
- Deployed to `_deploy/server-dev-8097` with backup folders retained
- Requires `ffmpeg` in `PATH` (found at `C:\Program Files\Jellyfin\Server\ffmpeg.exe`)
- Web dev server runs on port `8080` with proxy to backend on `8097`

### Production Deployment
To deploy to the Pi (KnightFlix container), follow `DEPLOY_PI_CODE_ONLY.md`:
1. Rebuild web UI: `npm run build:production`
2. Create Pi build tar
3. Build Docker image on Pi
4. Recreate KnightFlix container (data-safe)

---

## Files Changed/Added

### Backend (C# / .NET)
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/UserSeasonStats.cs` (new)
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/UserSeasonStatsConfiguration.cs` (new)
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs` (modified)
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260304100000_AddUserSeasonStats.cs` (new)
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/JellyfinDbModelSnapshot.cs` (modified)
- `jellyfin/MediaBrowser.Controller/Leaderboard/ILeaderboardService.cs` (new)
- `jellyfin/MediaBrowser.Controller/Leaderboard/LeaderboardInfo.cs` (new)
- `jellyfin/Jellyfin.Server.Implementations/Leaderboard/LeaderboardService.cs` (new)
- `jellyfin/Jellyfin.Api/Controllers/LeaderboardController.cs` (new)
- `jellyfin/Jellyfin.Api/Models/LeaderboardDtos/` (new folder with DTOs)
- `jellyfin/Jellyfin.Server/CoreAppHost.cs` (modified)
- `jellyfin/Jellyfin.Server.Implementations/Achievements/AchievementService.cs` (modified)

### Frontend (React/TypeScript)
- `jellyfin-web/src/utils/leaderboardApi.ts` (new)
- `jellyfin-web/src/apps/stable/routes/user/LeaderboardTab.tsx` (new)
- `jellyfin-web/src/apps/stable/routes/user/leaderboard.tsx` (new)
- `jellyfin-web/src/apps/stable/routes/user/leaderboard.scss` (new)
- `jellyfin-web/src/apps/stable/routes/user/achievements.tsx` (modified)
- `jellyfin-web/src/apps/stable/routes/asyncRoutes/user.ts` (modified)
- `jellyfin-web/src/apps/experimental/routes/asyncRoutes/user.ts` (modified)
- `jellyfin-web/src/components/toolbar/AppUserMenu.tsx` (modified)
- `jellyfin-web/src/apps/stable/routes/user/settings/index.tsx` (modified)

---

## Testing

### API Testing (verified via browser requests and direct endpoint checks)
```bash
# Personal stats
curl "http://127.0.0.1:8097/Leaderboard/Me?type=xp&api_key=<token>"

# Top leaderboard
curl "http://127.0.0.1:8097/Leaderboard/Top?type=watchtime&limit=10&offset=0&api_key=<token>"
```

### UI Testing
- Lint/style checks on updated leaderboard files passed
- Built and deployed local server to `:8097` using `deploy-8097-manual.bat`
- Verified in real browser session:
  - Achievements -> Leaderboard tab renders correctly
  - Metric switching triggers metric-specific `/Leaderboard/Me` + `/Leaderboard/Top` requests
  - Inline competition context appears in same table flow
  - Top 10 conditional placement logic applied in component render path

---

## Future Enhancements

- Add season selector dropdown (currently defaults to current year)
- Add historical season data (Past Seasons section)
- Add refresh/recompute button for stale stats
- Add explicit UI tests that seed >10 users for deterministic Top 10 / rank-10 edge validation
- Add optional compact badges/legend for competition strip semantics

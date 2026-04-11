# Progressive System Implementation Prompt (Handover)

Use this prompt when you want an AI to understand and safely extend the current Progressive System (XP, coins, achievements, overlays, milestone rewards, rank display, and request-redeem behavior) in this repo.

## Change Log

- 2026-03-03 | Jellyfin Web 10.12.0 (Seasonal Achievements)
  - Added yearly seasonal achievement model with preserved history (no deletion).
  - Seasonal migration currently marks 28 achievement definitions as yearly seasonal (100 total definitions -> 72 permanent + 28 seasonal).
  - Backend schema updates:
    - `AchievementDefinition.SeasonType` (for cadence, currently `yearly`).
    - `UserAchievements.SeasonYear` (null for permanent, year for seasonal rows).
    - Filtered unique indexes:
      - Permanent: unique by `UserId + AchievementId` when `SeasonYear IS NULL`.
      - Seasonal: unique by `UserId + AchievementId + SeasonYear` when `SeasonYear IS NOT NULL`.
  - Unlock/sync behavior updates:
    - Seasonal achievements can be re-earned each year.
    - Permanent achievements remain one-time unlocks.
    - XP and coins are granted on each seasonal re-unlock.
    - History remains fully preserved across years.
  - Achievements UI now groups history into:
    - `Permanent`
    - `Seasonal (Current Year)`
    - `Past Seasons` (collapsible, shown only when rows exist)
  - API payloads now include seasonal metadata:
    - Definition: `isSeasonal`, `seasonType`
    - User history/unlock rows: `isSeasonal`, `seasonType`, `seasonYear`

- 2026-03-03 | Jellyfin Web 10.12.0
  - Replaced fixed per-level XP with curve-based progression in `utils/levelRewards.ts`:
    - `getXpRequiredForLevel`
    - `getTotalXpForLevel`
    - `getLevelForTotalXp`
  - Switched Achievements tab level/progress/milestone XP calculations to curve-based totals.
  - Moved level-up trigger from Achievements page to global `LevelUpOverlayManager`.
  - Updated level-up UX:
    - Milestone level uses `CLAIM`.
    - Non-milestone levels auto-dismiss in 5s with `DISMISS`.
    - Removed cross dismiss icon.
  - Added milestone claim -> coin overlay visual trigger (`triggerCoinRewardOverlay`).
  - Added/confirmed rank badge behavior (`RANK: <emoji> <title>`) in Achievements header.
  - Added demo-user milestone test controls in Achievements tab (`baraibrothers`).
  - Refactored this handover prompt to reflect current architecture and contracts.

## Reusable Prompt

You are working on the Jellyfin Progressive System in this repository. First read and preserve the existing behavior before making any change.

### Primary goals

1. Keep XP, coin, level, and achievement flows consistent across overlays, history, Achievements tab, and Request tab.
2. Preserve current UX contract:
- Achievement unlock toast shows first.
- Coin gain overlay is shown after unlock flow when achievement grants coins.
- Level-up overlay is global (not Achievements-tab local).
- Achievements tab reflects live changes in XP/coins/recent unlocks.
- Request flow uses monthly quota first, then coin top-up when quota is exhausted.

### Current behavior to preserve

- Reward queue is centralized and event-driven.
- XP progression uses a curve, not fixed XP per level:
  - `XPRequired(level) = round(35 * level^1.2)`
  - Implemented in `utils/levelRewards.ts` via:
    - `getXpRequiredForLevel`
    - `getTotalXpForLevel`
    - `getLevelForTotalXp`
- Milestone coin rewards:
  - Levels: 10,20,30,40,50,60,70,80,90,100
  - Coins: 30,50,80,120,180,260,360,500,700,1000
  - Lifetime total: 3,280
- Level-up overlay behavior:
  - Triggered globally from reward events via `LevelUpOverlayManager`.
  - Milestone level: shows `CLAIM` and milestone coin amount.
  - Non-milestone level: shows quote, auto-dismiss in 5s, and `DISMISS`.
  - No cross/close icon.
  - Uses fixed full-screen overlay (`z-index: 9999`), confetti, and framer-motion.
  - On milestone `CLAIM`, coin overlay is triggered visually via `triggerCoinRewardOverlay`.
- Rank system:
  - 10 tiers for levels 1 to 100.
  - Displayed in Achievements header as `RANK: <emoji> <title>` with animated glow badge.
- Achievements and activity rewards both contribute to total XP and coin totals shown in the Achievements tab.
- Total coins shown in Achievements tab:
  - `achievement coins + activity coins + lifetime milestone coins`.
- Seasonal achievements:
  - Re-earnable each year (yearly cadence).
  - Current seasonal seed set is 28 IDs (via migration `20260303102000_AddSeasonalAchievementSupport`).
  - New unlock rows are stored per year (`SeasonYear`), old years remain visible.
  - XP and coin rewards are granted again for each new season unlock.
  - Do not delete old seasonal rows during reset.
- Request coin deduction happens only when submitting a request that exceeds monthly type quota.
- Coin visuals use the custom animated KF coin (SVG-based), not static material icon.

### Key architecture and files

- Reward root mount:
`jellyfin-web/src/RootApp.tsx`
`jellyfin-web/src/components/rewardSystem/RewardSystemRoot.tsx`

- Queue + event model:
`jellyfin-web/src/components/rewardSystem/RewardSystem.ts`
`jellyfin-web/src/components/rewardSystem/RewardQueueManager.tsx`

- Achievement overlay and API-triggered unlock/sync:
`jellyfin-web/src/components/rewardSystem/AchievementOverlayMount.ts`
`jellyfin-web/src/components/rewardSystem/AchievementOverlayContainer.tsx`

- Coin overlay and sequencing after achievements:
`jellyfin-web/src/components/rewardSystem/CoinRewardOverlay.tsx`
`jellyfin-web/src/components/rewardSystem/NotificationContainer.tsx`
`jellyfin-web/src/components/rewardSystem/FloatingItem.tsx`
`jellyfin-web/src/components/rewardSystem/Coin.tsx`
`jellyfin-web/src/components/rewardSystem/coinRewardOverlay.scss`

- Global level-up overlay:
`jellyfin-web/src/components/rewardSystem/LevelUpOverlayManager.tsx`
`jellyfin-web/src/components/rewardSystem/LevelUpOverlay.tsx`
`jellyfin-web/src/components/rewardSystem/levelUpOverlay.scss`
`jellyfin-web/src/utils/quotes.ts`

- XP curve + rank + milestone reward utilities:
`jellyfin-web/src/utils/levelRewards.ts`

- Local history stores (drives live UI totals):
`jellyfin-web/src/components/rewardSystem/achievementHistoryStore.ts`
`jellyfin-web/src/components/rewardSystem/activityRewardHistoryStore.ts`
`jellyfin-web/src/components/rewardSystem/coinSpendStore.ts`

- Achievements tab (HUD + rank + recent unlocks + realtime sync):
`jellyfin-web/src/apps/stable/routes/user/achievements.tsx`
`jellyfin-web/src/apps/stable/routes/user/achievements.scss`
`jellyfin-web/src/components/rewardSystem/RankBadge.tsx`

- Request tab coin wallet + redeem behavior:
`jellyfin-web/src/apps/stable/routes/request/index.tsx`
`jellyfin-web/src/components/contentRequests/RequestForm.tsx`
`jellyfin-web/src/components/contentRequests/contentRequests.scss`
`jellyfin-web/src/strings/en-us.json`

- Content request server logic:
`jellyfin/Jellyfin.Server.Implementations/ContentRequests/ContentRequestService.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/ContentRequest.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/ContentRequestConfiguration.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260302150000_AddServerCoinRedeemTracking.cs`

- Achievement seasonal server logic:
`jellyfin/Jellyfin.Server.Implementations/Achievements/AchievementService.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/AchievementDefinition.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/UserAchievement.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/AchievementDefinitionConfiguration.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/UserAchievementConfiguration.cs`
`jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260303102000_AddSeasonalAchievementSupport.cs`

### Achievements tab realtime contract

- Live updates from local reward history subscriptions:
  - XP bar and total XP update.
  - Level and rank badge update.
  - Coin total updates.
  - Recent achievements list/sections update when new unlock is added.
  - Next milestone `XP to unlock` updates from curve-based cumulative XP.

- Periodic server history reconciliation:
  - Visible tab: sync every 20s.
  - Hidden tab: sync every 60s.
  - Immediate sync on tab becoming visible.

- Dummy QA controls are intentionally present for demo user:
  - `Prepare Milestone Test`
  - `Trigger Milestone Level-Up`
  - Shown when username is `baraibrothers`.

### Request tab redeem contract

- Base monthly quota starts at Movie=5, Series=2 (active subscription cycle).
- If selected type has remaining quota, request consumes quota and does not deduct coins.
- If selected type quota is exhausted, request requires coin top-up:
  - Movie top-up: 200 coins.
  - Series top-up: 400 coins.
- Coin deduction is applied only in that top-up scenario.
- Backend validates and enforces this rule server-side on `POST /Request`.
- Server computes available coins as:
  - Earned = sum of unlocked achievement coin rewards (`UserAchievements` x `AchievementDefinition`).
  - Spent = sum of `ContentRequests.CoinRedeemCost`.
  - If available coins are insufficient for top-up, request is rejected with `409 Conflict`.

- Wallet UI requirements:
  - Collapsible details section before request form.
  - Animated KF coin in summary row.
  - Shows available, lifetime, spent, redeem costs, current selection state.
  - Shows insufficient balance alert when relevant.

### Overlay sequencing contract

- Achievement overlay should take visual priority.
- Coin overlay for achievement reward should not overlap active achievement overlay.
- Activity-only coin gains can display independently via reward events.
- Global level-up overlay is independent from Achievements route lifecycle.
- Milestone `CLAIM` triggers coin overlay visual pulse (does not alter reward queue event names).

### Known limitations and technical debt

- Coin spend is persisted server-side (`ContentRequests.CoinRedeemCost`) and enforced at request creation.
- Frontend still keeps a local spend mirror (`coinSpendStore`) for immediate UI state; treat this as UX cache, not source of truth.
- Activity-reward coin earnings are still client-driven history; server-authoritative earned coins currently come from achievements.
- Existing requests created before the server coin migration default to `CoinRedeemCost = 0` (no historical backfill of legacy client-only spend).
- Legacy/local seasonal history rows without `seasonYear` are treated as current season in UI grouping.
- Current XP curve implementation (`getXpRequiredForLevel(level) = round(35 * level^1.2)` and cumulative sum in `getTotalXpForLevel`) produces about 404,019 total XP to reach level 100. Rebalance centrally in `levelRewards.ts` if target pacing changes.

### Safe enhancement checklist

1. Do not break `RewardSystem` event names and queue semantics.
2. Keep overlay sequencing logic intact while changing visuals.
3. Do not reintroduce fixed XP-per-level math; use `levelRewards.ts` curve helpers.
4. Maintain Achievements tab realtime subscriptions and periodic sync.
5. Preserve Request top-up rule: quota first, coins only after exhaustion.
6. Update `en-us.json` when changing visible copy.
7. Run:
  - `npm run build:check` in `jellyfin-web`
  - `npm run build:production` in `jellyfin-web`
  - `dotnet build jellyfin/Jellyfin.Server.Implementations/Jellyfin.Server.Implementations.csproj -c Debug` for server-side edits
  - Apply DB migration `20260302150000_AddServerCoinRedeemTracking` before validating request coin enforcement.
  - Apply DB migration `20260303102000_AddSeasonalAchievementSupport` before validating seasonal unlock/history behavior.

### Manual QA quick plan

1. Login with test user `baraibrothers` / `prnrr123`.
2. Open Achievements (`#/achievements`) and verify:
  - XP/level/rank/progress update after reward injection.
  - History sections render correctly (`Permanent`, `Seasonal (Current Year)`, `Past Seasons` only when available).
  - Server sync requests continue periodically.
3. Milestone overlay test:
  - Click `Prepare Milestone Test`.
  - Click `Trigger Milestone Level-Up`.
  - Confirm level-up overlay appears.
  - Click `CLAIM` at milestone and confirm coin overlay appears.
  - Confirm total coins includes milestone lifetime sum for current level.
4. Open Request tab and verify:
  - Wallet collapse/expand, animated coin icon, disclaimer bullets.
  - Free quota submission does not spend coins.
  - Quota-exhausted submission spends coins if balance is enough.
  - Type toggles reflect quota/balance availability.
5. API-level validation checks:
  - Exhaust Movie quota with low server coins -> `POST /Request` returns `409`.
  - Seed enough achievement coins -> first quota-top-up request succeeds and stores `CoinRedeemCost` (Movie=200, Series=400).

### Important deployment note

- If server DLL replacement fails due process lock on Windows, stop Jellyfin with elevated permissions before copying updated assemblies into deployment folder.

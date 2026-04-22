# Request System Implementation Report

Date: 2026-02-26

## April 22, 2026 Follow-Up Fixes (Web Push Subscription Stability)

Observed production issue:
- `RequestController.SubscribeWebPush` could fail in `ContentRequestWebPushService.UpsertSubscription` due to duplicate endpoint rows and/or concurrent upsert races.

Root cause:
- Subscription upsert assumed a single endpoint row and did not explicitly self-heal duplicate endpoint data before update/save.
- Concurrent insert/update calls for the same endpoint could surface EF update exceptions instead of gracefully converging to one record.

Fix applied:
- Upsert now loads all rows for the endpoint, keeps the newest row, and removes duplicates in the same transaction.
- Added race-safe retry behavior: when concurrent writes conflict, service re-reads endpoint rows, updates survivor row, removes extras, and saves again.
- Result is a stable endpoint record without surfacing transient DB write failures to the API caller.

## Scope Completed

Implemented a full Request System across server, database, web user UI, web admin UI, and notification UX.

Main capability delivered:

- Users can submit movie/series requests with subscription-aware caps.
- Users can view/search their own requests and remaining quota.
- Admin can review pending requests, approve/reject them, and complete approved requests by linking a Jellyfin item.
- Users receive completion popups and can mark notifications viewed.
- Admin sees an unseen-pending indicator in Dashboard navigation.
- Request UI was refactored non-breakingly into modular shared components with responsive table/card rendering and Grace/Expired UX handling.
- Follow-up UI behavior pass completed for web/mobile alignment: robust request-id parsing, mobile card enforcement in WebView layout mode, and overflow-safe 320px rendering.

## Repositories/Areas Touched

- `jellyfin` (API, business logic, DB entity + migration, service registration, integration tests)
- `jellyfin-web` (user route, admin route, API client utility, menu/nav wiring, notification popup, strings, GIF assets)

No request-system changes were made in `jellyfin-android`, `jellyfin-androidtv`, or `jellyfin-desktop`.

## Backend Implementation (`jellyfin`)

### API Controller

- Added `jellyfin/Jellyfin.Api/Controllers/RequestController.cs`
- Base route: `Request`
- Endpoints:
1. `POST Request` (create request; auth required)
2. `GET Request/My` (my requests + quota; auth required)
3. `GET Request/Public` (public rows, paged; auth required)
4. `GET Request/Notifications` (completed rows still eligible for popup; auth required)
5. `POST Request/NotificationViewedBulk` (increments notification count for owned rows; auth required)
6. `GET Request/Admin` (all admin rows + marks unseen pending as viewed; elevation required)
7. `GET Request/Admin/UnseenPendingCount` (count pending rows where `IsAdminViewed=false`; elevation required)
8. `POST Request/Admin/Approve` (pending -> approved; elevation required)
9. `POST Request/Admin/Reject` (pending/approved -> rejected; elevation required)
10. `POST Request/Admin/Complete` (approved -> completed with `JellyfinItemId`; elevation required)

### Service Contract + Exceptions

- Added `MediaBrowser.Controller.ContentRequests` contract namespace:
- `IContentRequestService.cs`
- `ContentRequestInfo.cs`
- `ContentRequestListResult.cs`
- `MyContentRequestsResult.cs`
- `ContentRequestQuotaInfo.cs`
- `ContentRequestType.cs`
- `ContentRequestStatus.cs`
- `ContentRequestConflictException.cs`
- `ContentRequestInactiveSubscriptionException.cs`
- `ContentRequestNotFoundException.cs`

### Service Implementation

- Added `jellyfin/Jellyfin.Server.Implementations/ContentRequests/ContentRequestService.cs`
- Registered in DI:
- `jellyfin/Jellyfin.Server/CoreAppHost.cs`
- `serviceCollection.AddSingleton<IContentRequestService, ContentRequestService>();`

Implemented business rules:

- Request caps per active subscription cycle:
- Movies: `5`
- Series: `2`
- Cap usage counting includes status:
- `Pending`
- `Approved`
- `Completed` (Fulfilled in UI)
- Rejected rows are excluded from usage counting
- Series requests must provide `SeasonNumber > 0`
- Duplicate blocking is global for active rows (`Pending` or `Approved`) using normalized title + type
- Title normalization:
- trim
- collapse whitespace
- lowercase invariant
- Creation requires active subscription:
- resolved from redeemed access-key start date + user expiry fallback
- invalid/inactive returns forbidden conflict path via controller mapping
- Workflow transitions allowed:
- `Pending -> Approved`
- `Pending -> Rejected`
- `Approved -> Completed`
- `Approved -> Rejected`
- Any other transition throws conflict
- Public list excludes rejected rows (returns Pending/Approved/Completed)
- Notifications include only completed rows with `NotificationCount < 2`
- Bulk notification viewed increments only matching owned completed rows
- Admin unseen behavior:
- count is pending rows with `IsAdminViewed=false`
- calling `GET Request/Admin` marks those unseen pending rows as viewed

## Database Implementation (`jellyfin`)

- Added entity:
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/ContentRequest.cs`
- Added DB enums:
- `.../Enums/ContentRequestType.cs`
- `.../Enums/ContentRequestStatus.cs`
- Added model config:
- `.../ModelConfiguration/ContentRequestConfiguration.cs`
- Added `DbSet`:
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`
- `public DbSet<ContentRequest> ContentRequests => Set<ContentRequest>();`
- Added migration:
- `.../Migrations/20260226075513_AddContentRequests.cs`
- `.../Migrations/20260226075513_AddContentRequests.Designer.cs`
- Snapshot updated:
- `.../Migrations/JellyfinDbModelSnapshot.cs`

Table created: `ContentRequests`

Columns:

- `Id` (PK, GUID)
- `UserId` (FK -> `Users.Id`, cascade delete)
- `Title` (max 255)
- `NormalizedTitle` (max 255)
- `Type` (int enum)
- `SeasonNumber` (nullable int)
- `RequestedAt` (UTC DateTime)
- `Status` (int enum)
- `JellyfinItemId` (nullable GUID)
- `NotificationCount` (default `0`)
- `IsAdminViewed` (default `false`)

Indexes:

- `IX_ContentRequests_UserId`
- `IX_ContentRequests_Status`
- `IX_ContentRequests_IsAdminViewed`
- `IX_ContentRequests_UserId_Type_Status`
- `IX_ContentRequests_NormalizedTitle`

## API DTO Layer (`jellyfin`)

Added DTOs under:

- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/`

Files:

- `CreateContentRequestRequest.cs`
- `ContentRequestRowDto.cs`
- `PublicContentRequestRowDto.cs`
- `PublicContentRequestListResponse.cs`
- `MyContentRequestsResponse.cs`
- `ContentRequestCapSummaryDto.cs`
- `AdminRequestActionRequest.cs`
- `AdminCompleteContentRequestRequest.cs`
- `AdminUnseenPendingCountResponse.cs`
- `BulkNotificationViewedRequest.cs`

## Web Implementation (`jellyfin-web`)

### Request API Utility

- Added `jellyfin-web/src/utils/contentRequestsApi.ts`
- Handles all request-system endpoints and maps server enums/status values.
- Supports both PascalCase and camelCase response keys for resilience.

### UI Refactor (Non-Breaking, Modular)

- Added shared component layer in `jellyfin-web/src/components/contentRequests/`:
- `RequestPageContainer.tsx`
- `RequestHeader.tsx`
- `RequestStatusBadge.tsx`
- `RequestEmptyState.tsx`
- `RequestTable.tsx`
- `RequestCard.tsx`
- `RequestQuotaSummary.tsx`
- `RequestForm.tsx`
- `RequestList.tsx`
- `AdminRequestActions.tsx`
- `AdminRequestTable.tsx`
- `AdminCompleteModal.tsx`
- `types.ts` (`RequestSubscriptionUiState = 'active' | 'grace' | 'expired'`)
- Added shared stylesheet:
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`
- Route paths remain unchanged:
- user `#/request`
- admin `/dashboard/requests`
- No backend/request API contract changes in this refactor.

### User-Facing Request Page

- Refactored route page (same route path):
- `jellyfin-web/src/apps/stable/routes/request/index.tsx`
- `jellyfin-web/src/apps/stable/routes/request/request.scss`

Features:

- Create request form for Movie/Series
- Series season validation
- Quota display (remaining movies/series)
- UI subscription states from current user:
- Active: submit enabled
- Grace: submit visible but disabled, with message
- Expired: submit hidden, renewal CTA to `#/subscription`
- Search/filter over user rows (local only)
- Responsive behavior:
- desktop table on >=768px
- card layout on <768px (no horizontal overflow)
- card layout is also forced when `layoutManager.mobile` is active (prevents desktop table fallback in mobile WebView scale modes)
- Status badge display mapping:
- backend `Completed` displays as `Fulfilled` in UI
- Header/content overlap protection via flow container (`content-primary` + request container)

Latest behavior-alignment updates (2026-02-26):

- Request title parsing now extracts Request ID from both:
- newline format (`TITLE \n 123456...`)
- same-line trailing numeric format (`TITLE 123456...`)
- Desktop table scanability tuned:
- Title column truncates with ellipsis (`requestCellTruncate`) for consistent row height
- Request ID stays in dedicated column
- Date column right-aligned, status centered
- Mobile rendering hardened:
- cards are used for request rows under mobile layout mode
- quota pills keep equal-width rendering with `min-width: 0` and wrap-safe text
- 320px view verified without horizontal overflow
- Non-functional UI cleanup:
- replaced corrupted banner/empty-state icon glyphs with stable ASCII-safe markers

Navigation wired from:

- `jellyfin-web/src/apps/stable/routes/asyncRoutes/user.ts`
- `jellyfin-web/src/apps/experimental/routes/asyncRoutes/user.ts`
- `jellyfin-web/src/scripts/libraryMenu.js`
- `jellyfin-web/src/apps/stable/routes/user/settings/index.tsx`

### Admin Request Management Page

- Refactored route page (same route path):
- `jellyfin-web/src/apps/dashboard/routes/requests/index.tsx`
- `jellyfin-web/src/apps/dashboard/routes/requests/requests.scss`

Dashboard wiring:

- `jellyfin-web/src/apps/dashboard/routes/_asyncRoutes.ts`
- Added drawer item in:
- `jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx`

Features:

- List all rows with username/title/type/season/date/status
- Actions:
- approve pending
- reject pending/approved
- complete approved with library search + confirm modal
- Uses modular UI components:
- `AdminRequestTable` (desktop table + mobile cards)
- `AdminRequestActions` (wrapped action controls)
- `AdminCompleteModal` (search -> select -> confirm Yes/No)
- Status badges reuse shared mapping (`Completed` -> `Fulfilled` display)
- Prevents column clipping/overflow in narrow viewports
- Uses React Query hooks:
- `src/apps/dashboard/features/contentRequests/api/queryKeys.ts`
- `src/apps/dashboard/features/contentRequests/api/useAdminContentRequests.ts`
- `src/apps/dashboard/features/contentRequests/api/useAdminUnseenPendingCount.ts`

### Completion Notification Popup

- Added component:
- `jellyfin-web/src/components/contentRequests/RequestNotificationPopup.tsx`
- `jellyfin-web/src/components/contentRequests/RequestNotificationPopup.scss`

Mounted in:

- `jellyfin-web/src/apps/stable/AppLayout.tsx`
- `jellyfin-web/src/apps/experimental/AppLayout.tsx`

Behavior:

- Pulls notification rows from `GET Request/Notifications`
- Fetches item details + poster for linked `JellyfinItemId`
- Presents pop-up cards (mobile/tv/web responsive classes)
- "Watch Now" opens item page and closes popup
- Close action bulk-marks viewed via `POST Request/NotificationViewedBulk`
- Notification logic and API behavior preserved in the refactor (no functional changes)

### GIF Assets Added (Request System)

Added:

- `jellyfin-web/src/assets/branding/admin-request-badge.gif`
- `jellyfin-web/src/assets/branding/request-popup-accent.gif`

Usage points:

- `admin-request-badge.gif` used in Dashboard drawer request nav badge:
- `jellyfin-web/src/apps/dashboard/components/drawer/sections/ServerDrawerSection.tsx`
- `request-popup-accent.gif` used in notification popup header:
- `jellyfin-web/src/components/contentRequests/RequestNotificationPopup.tsx`

### Web Typings + Strings

- Added `.gif` module declaration:
- `jellyfin-web/src/index.d.ts`
- Added request-system strings:
- `jellyfin-web/src/strings/en-us.json`
- Added/updated string keys for refactor UX:
- `RequestStatusFulfilled`
- `RequestGraceMessage`
- `RequestExpiredRenewCta`
- `RequestExpiredMessage`
- `RequestCompleteConfirmYes`
- `RequestCompleteConfirmNo`

## Integration Tests Added (`jellyfin`)

Added test suite:

- `jellyfin/tests/Jellyfin.Server.Integration.Tests/Controllers/ContentRequestControllerTests.cs`

Coverage includes:

1. Movie cap enforced at 5
2. Duplicate blocking for pending/approved
3. Previous-cycle requests ignored for current-cycle cap
4. Public list excludes rejected rows
5. Notifications include only completed rows with `NotificationCount < 2`
6. Bulk notification viewed increments only owned rows
7. Admin fetch marks unseen pending rows as viewed
8. Unseen pending count decreases after admin fetch
9. Completed rows count toward movie cap within the cycle
10. Quota usage (`Used/Remaining`) counts completed rows

## Validation Run (Current Workspace)

Observed from logs in repo root:

- `dotnet-build.log`: build succeeded, `0 Warning(s)`, `0 Error(s)`
- `dotnet-tests-build.log`: build succeeded, `0 Error(s)`
- `dotnet-tests-run.log`: integration tests passed `8/8`
- `npm-build-production.log`: production build compiled with `2 warnings`
- Re-run after UI refactor: `npm run build:production` passed (same 2 webpack size warnings, no compile errors)
- Re-run after behavior-alignment patch: `npm run build:production` passed (same 2 webpack size warnings, no compile errors)
- Re-run after completed-cap usage patch:
- `dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj -c Release --filter FullyQualifiedName~ContentRequestControllerTests`
- result: `Passed: 10, Failed: 0` (10/10)
- Playwright validation (web + mobile viewport) executed on `#/request` with:
- desktop `1366x768`
- mobile `390x844`
- mobile `320x844`
- Overflow checks:
- `document.documentElement.scrollWidth === window.innerWidth` at `1366` and `320` (no horizontal overflow)
- Request page API behavior observed during verification:
- page load calls `GET /Request/My`
- local search/filter sends no additional request-system API calls
- Console/runtime note during checks:
- recurring `GET /Branding/Logo` `404` seen from existing branding config; not introduced by request-flow refactor
- `android-assembleDebug.log`: build successful
- `androidtv-assembleDebug.log`: build successful

## Visual Verification Artifacts

Captured images in repo root:

- `request-user-tab.png`
- `request-admin-tab.png`
- `request-popup-web.png`
- `request-popup-mobile.png`
- `request-popup-tv.png`
- Additional validation performed through Playwright viewport captures for the follow-up behavior patch (desktop/mobile/320px); images were generated during session for verification.

## Upgrade Notes (For Future AI/Dev Work)

- Keep migration `20260226075513_AddContentRequests` when rebasing/upgrading DB layer.
- Preserve DI registration for `IContentRequestService`.
- Preserve route wiring for:
- user `#/request`
- dashboard `/dashboard/requests`
- Preserve popup mount in app layouts; otherwise completion notifications will silently disappear.
- Keep both request GIF asset paths unchanged unless intentionally replacing assets.
- Preserve shared request UI components under `src/components/contentRequests/` when extending user/admin request pages.
- Keep `Completed -> Fulfilled` as display-only mapping in UI; do not rename backend enum/status.
- Keep `layoutManager.mobile`-aware fallback in request list rendering; this is required for mobile WebView environments that do not always trip width-only CSS breakpoints.
- Keep request title parsing tolerant to historical title formats that append numeric IDs to title text.
- Keep cap-usage counting aligned with workflow semantics:
- cap usage includes `Pending`, `Approved`, and `Completed` rows in the active cycle
- `Rejected` rows do not consume cap
- If changing caps, adjust:
- service constants (`_movieCap`, `_seriesCap`)
- UI text/expectations if needed
- tests relying on cap values
- If changing workflow states, update:
- service transition guard
- admin actions UI
- status badges/styles
- integration tests

## Post-Report Addendum (2026-02-26, later updates)

### Mobile Alignment Hardening (Request Tab)

Additional UI-hardening adjustments were applied in `jellyfin-web` to address Android/WebView-specific alignment and overflow edge cases without changing request-system behavior:

- Updated shared request styles in:
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`
- Input/control sizing normalization:
- ensured request controls use `box-sizing: border-box`
- added shrink safety (`min-width: 0`) on key grid/flex children
- kept controls at consistent min height for better alignment (`Title`, `Season`, `Search`, submit/toggle controls)
- Focus-state rendering refinement:
- replaced outside-offset focus outline treatment with in-boundary focus styling (border + inset ring) to avoid "double-box" appearance and clipping artifacts
- Overflow/clipping safety:
- removed request-container X-axis clipping that could cut focus visuals or control edges in narrow mobile layouts
- Mobile layout robustness:
- enforced single-column form layout under mobile/touch criteria
- quota pills stack cleanly on narrow widths (`<=560px`) to prevent text clipping/overlap
- toggle labels constrained for narrow widths (nowrap/ellipsis behavior)

### Mobile Card-Mode Detection Hardening

- Updated:
- `jellyfin-web/src/components/contentRequests/useRequestIsMobileLayout.ts`
- Added fallback checks using `visualViewport` width and touch capability (`navigator.maxTouchPoints`) in addition to existing layout mode/class/media-query checks.
- Added listeners for orientation/viewport-size changes so card mode remains deterministic on Android/WebView scale/layout transitions.

### Navigation Deduplication (Mobile Left Slide)

Removed duplicate `Request` nav entry from mobile left-side drawers, while keeping Request available from the profile/user menu:

- Removed from:
- `jellyfin-web/src/scripts/libraryMenu.js`
- `jellyfin-web/src/apps/experimental/components/drawers/MainDrawerContent.tsx`
- Retained in:
- `jellyfin-web/src/components/toolbar/AppUserMenu.tsx`

This prevents showing the same Request destination in both mobile side drawer and profile menu.

### Validation Addendum

- Re-run after these updates:
- `npm run build:production` passed (same existing 2 webpack size warnings; no compile errors).

## Status Snapshot

As of this note, request-system changes are present in working tree and not yet committed.

## Post-Report Addendum (2026-04-12, mobile request availability notifications)

### Scope

Implemented Android mobile alerts for completed content requests so users receive native notifications when requested content becomes available.

Added in `jellyfin-android`:

- `app/src/main/java/org/jellyfin/mobile/requests/ContentRequestNotificationManager.kt`
- `app/src/main/res/drawable/ic_notification_request_movie.xml`
- `app/src/main/res/drawable/ic_notification_request_series.xml`
- Notification string resources in:
- `app/src/main/res/values/strings.xml`
- Wiring updates in:
- `app/src/main/java/org/jellyfin/mobile/MainActivity.kt`
- `app/src/main/java/org/jellyfin/mobile/webapp/WebViewFragment.kt`
- `app/src/main/java/org/jellyfin/mobile/webapp/JellyfinWebViewClient.kt`
- `app/src/main/java/org/jellyfin/mobile/app/AppModule.kt`
- `app/src/main/java/org/jellyfin/mobile/app/AppPreferences.kt`
- `app/src/main/java/org/jellyfin/mobile/utils/Constants.kt`

### Behavior implemented

- Poll source:
- uses existing backend endpoint `GET Request/Notifications` (authenticated via `api_key`) from mobile app.
- Poll cadence:
- one immediate sync when user session is established in `JellyfinWebViewClient` (`sessions/capabilities/full` path).
- repeating sync every `120000ms` while `WebViewFragment` is resumed.
- Notification delivery:
- native Android channel `org.jellyfin.mobile.request.CONTENT_READY` (high importance).
- server rows are marked viewed after successful local notification display via `POST Request/NotificationViewedBulk`.
- this prevents repeated alerts for the same request rows.

### Notification UX differentiation (Movie vs Series)

- Movie:
- title: `[MOVIE] Request Ready`
- body: `"Movie Name" is now available to stream. Tap to open.`
- icon: `ic_notification_request_movie`
- image preference: item `PRIMARY` image (poster-style).
- Series:
- title: `[SERIES] Request Ready`
- body:
- with season: `"Series Name" is now available. Season N is ready to stream. Tap to open.`
- without season: `"Series Name" is now available to stream. Tap to open.`
- icon: `ic_notification_request_series`
- image preference: item `BACKDROP`, fallback to `PRIMARY`.

### Tap/open flow (requested by product direction)

- Notification tap includes `EXTRA_REQUEST_CONTENT_ITEM_ID`.
- Main activity stores tapped item id in persistent app preferences (`pendingRequestContentItemId`) and attempts immediate navigation.
- If user is logged in and web app router is ready:
- app opens the exact content detail page via `window.appRouter.showItem(itemId, serverId)`.
- If user is not logged in (or router/server context not ready):
- pending item id is retained.
- after successful authentication, `WebViewFragment` retries navigation automatically and clears pending state on success.
- Result:
- no generic library landing; opens the specific requested movie/series detail page when session is valid.

### Permission handling

- `MainActivity` now requests `POST_NOTIFICATIONS` on Android 13+ at app startup (if not already granted).

### Validation

- Ran:
- `./gradlew assembleDebug` in `jellyfin-android`
- Result:
- build successful for both `libreDebug` and `proprietaryDebug`.

### Operational note for future work

- Current mobile alerts are app-side polling based and do not introduce FCM/APNs server push infrastructure.
- Users receive these notifications when the app process is active/resumed and session is available; a future push pipeline can be layered later without changing request API contracts.

## Post-Report Addendum (2026-04-19, request pool visibility + admin reward quota)

### Scope

Implemented two new request-system capabilities across API, service logic, DB schema, web user UI, and web admin UI:

- User Request tab now includes a searchable/filterable "Current Request Pool" of requests from other users.
- Admin Request tab now includes "Reward Request Quota" tooling to grant extra Movie/Series request counts to any user with debounced username suggestion and confirmation flow.

### Backend/API changes (`jellyfin`)

Updated:

- `jellyfin/Jellyfin.Api/Controllers/RequestController.cs`
- `jellyfin/Jellyfin.Server.Implementations/ContentRequests/ContentRequestService.cs`
- `jellyfin/MediaBrowser.Controller/ContentRequests/IContentRequestService.cs`
- `jellyfin/MediaBrowser.Controller/ContentRequests/ContentRequestQuotaInfo.cs`

Added DTOs:

- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/AdminContentRequestUserSuggestionDto.cs`
- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/AdminContentRequestUserQuotaResponse.cs`
- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/AdminRewardContentRequestQuotaRequest.cs`

Added contract models:

- `jellyfin/MediaBrowser.Controller/ContentRequests/ContentRequestUserSuggestion.cs`
- `jellyfin/MediaBrowser.Controller/ContentRequests/ContentRequestAdminUserQuotaResult.cs`

Adjusted DTOs:

- `PublicContentRequestRowDto` now includes requester identity fields:
- `UserId`
- `Username`
- `ContentRequestCapSummaryDto` and contract quota now include:
- `RewardMovies`
- `RewardSeries`

New admin endpoints:

1. `GET Request/Admin/UserSuggestions?query=<text>&take=<n>`
- partial username search for autosuggest.
2. `GET Request/Admin/UserQuota?userId=<guid>`
- returns selected user's current quota state and reward balances.
3. `POST Request/Admin/RewardQuota`
- payload: `{ UserId, MovieCount, SeriesCount }`
- grants extra request slots.

### Reward quota behavior

Added persistent reward-balance store:

- Entity: `ContentRequestRewardBalance`
- Files:
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/ContentRequestRewardBalance.cs`
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/ModelConfiguration/ContentRequestRewardBalanceConfiguration.cs`
- `DbSet` added in:
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`

Migration added:

- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260418215347_AddContentRequestRewardBalances.cs`
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/20260418215347_AddContentRequestRewardBalances.Designer.cs`
- Snapshot updated:
- `jellyfin/src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite/Migrations/JellyfinDbModelSnapshot.cs`

Runtime semantics:

- Base monthly cap is still enforced first (`Movie=5`, `Series=2`).
- If cap is exhausted and user has reward balance, one reward slot is consumed.
- Coin top-up is charged only when both base cap and reward balance are exhausted.
- `Request/My` quota now reports reward balances explicitly (`RewardMovies`, `RewardSeries`).

### User Request tab UI (`jellyfin-web`)

Added:

- `jellyfin-web/src/components/contentRequests/PublicRequestPool.tsx`

Integrated into:

- `jellyfin-web/src/apps/stable/routes/request/index.tsx`

Behavior:

- Fetches from existing `GET Request/Public`.
- Shows requests from other users in a native request-table/card section.
- Includes:
- search box
- type filter (All/Movie/Series)
- scope filter (`Current` = Pending+Approved, or `All`)
- Uses existing request styles so it matches current page design.

### Admin Request tab UI (`jellyfin-web`)

Added:

- `jellyfin-web/src/components/contentRequests/AdminRewardQuotaManager.tsx`

Integrated into:

- `jellyfin-web/src/apps/dashboard/routes/requests/index.tsx`

Behavior:

- Username autosuggest with **2-second idle debounce** before API search.
- Partial match suggestions from `Request/Admin/UserSuggestions`.
- On user selection, loads current quota counts from `Request/Admin/UserQuota`.
- Admin can add Movie/Series counts via number input + quick-pick dropdown.
- Add button enables only when:
- a user is selected
- at least one count is > 0
- confirmation dialog accepted
- Success/failure handled with toast feedback.

Supporting updates:

- `jellyfin-web/src/utils/contentRequestsApi.ts` (new admin methods + expanded models)
- `jellyfin-web/src/components/contentRequests/contentRequests.scss` (new panel/filter styles)
- `jellyfin-web/src/strings/en-us.json` (new request-pool/admin-reward strings)

### Tests and validation

Updated integration tests:

- `jellyfin/tests/Jellyfin.Server.Integration.Tests/Controllers/ContentRequestControllerTests.cs`

New/expanded coverage:

1. Public request rows include requester identity (`UserId` + `Username`).
2. Admin user suggestion endpoint returns partial username matches.
3. Admin reward quota grant increases remaining counts and allows post-cap request without coin charge while consuming reward balance.

Executed validation:

- `dotnet build Jellyfin.Server.Implementations/Jellyfin.Server.Implementations.csproj -c Release` passed.
- `dotnet build Jellyfin.Api/Jellyfin.Api.csproj -c Release` passed.
- `dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj -c Release --filter FullyQualifiedName~ContentRequestControllerTests` passed (`15/15`).
- `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --filter FullyQualifiedName~EfMigrationTests` passed (`1/1`).
- `npm run build:production` in `jellyfin-web` passed (existing bundle-size warnings only).

## Post-Report Addendum (2026-04-19, pagination + layout refinement pass)

### Scope

Applied the requested follow-up UX/data-loading refinements:

- User Request page:
- improved table readability/spacing
- made top panels balanced
- moved `Current Request Pool` to full-width section below
- added server-side pagination for `My Requests` (10 rows/page)
- Admin Request page:
- changed reward username search placeholder text to shorter label
- added server-side pagination for Request Management (10 rows/page)

### Backend/API updates (`jellyfin`)

Updated service contract:

- `jellyfin/MediaBrowser.Controller/ContentRequests/IContentRequestService.cs`
- Added:
- `GetMyRequestsPaged(Guid userId, int skip, int take)`
- `GetAdminRequestsPaged(int skip, int take)`

Updated implementation:

- `jellyfin/Jellyfin.Server.Implementations/ContentRequests/ContentRequestService.cs`
- Implemented both paged methods with:
- stable descending order by `RequestedAt`
- `Skip/Take` slicing
- total count return for page controls
- Reused unseen-pending mark-as-viewed behavior for admin via shared helper so existing semantics remain unchanged.

Added API DTOs:

- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/MyContentRequestsPagedResponse.cs`
- `jellyfin/Jellyfin.Api/Models/ContentRequestDtos/AdminContentRequestListResponse.cs`

Updated controller:

- `jellyfin/Jellyfin.Api/Controllers/RequestController.cs`
- Added endpoints:
1. `GET Request/My/Paged?skip=<n>&take=<n>`
2. `GET Request/Admin/Paged?skip=<n>&take=<n>`

### Web updates (`jellyfin-web`)

API utility changes:

- `jellyfin-web/src/utils/contentRequestsApi.ts`
- Added paged client calls:
- `getMyContentRequestsPaged(...)`
- `getAdminContentRequestsPaged(...)`

New shared component:

- `jellyfin-web/src/components/contentRequests/RequestPagination.tsx`
- Prev/Next controls + page label.

User Request page updates:

- `jellyfin-web/src/apps/stable/routes/request/index.tsx`
- `jellyfin-web/src/components/contentRequests/RequestList.tsx`
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`
- Changes:
- `My Requests` now pulls only current page from server (10/page).
- page index resets to first page after successful new submit.
- `My Requests` and `Submit a New Request` columns are balanced to same width.
- `Current Request Pool` moved below top grid to use full page width for better row text visibility.
- widened title-area allocation to reduce wrapping/truncation pressure.

Admin Request page updates:

- `jellyfin-web/src/apps/dashboard/routes/requests/index.tsx`
- `jellyfin-web/src/components/contentRequests/AdminRequestTable.tsx`
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`
- Changes:
- Request Management now loads server-paged rows (10/page).
- pagination controls added below admin table.

String updates:

- `jellyfin-web/src/strings/en-us.json`
- `RequestAdminRewardUserPlaceholder` changed from:
- `Type username to search (API runs after 2 seconds idle)`
- to:
- `Type username to search`
- Added `RequestPaginationPageLabel` for shared paginator text.

### Tests and validation

Updated integration tests:

- `jellyfin/tests/Jellyfin.Server.Integration.Tests/Controllers/ContentRequestControllerTests.cs`
- Added coverage for:
1. `My/Paged` slicing + total count contract
2. `Admin/Paged` slicing + total count contract

Validation run:

- `dotnet build jellyfin/Jellyfin.Server.Implementations/Jellyfin.Server.Implementations.csproj -c Release` passed.
- `dotnet build jellyfin/Jellyfin.Api/Jellyfin.Api.csproj -c Release` passed.
- `dotnet test jellyfin/tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj -c Release --filter FullyQualifiedName~ContentRequestControllerTests` passed (`17/17`).
- `npm run build:production` in `jellyfin-web` passed (existing webpack size warnings only, no compile errors).

### Before vs after behavior summary

- Before:
- user/admin request tables depended on full-list fetches and local rendering, causing crowding and unnecessary data load as rows grow.
- user top layout left limited width for newly introduced request-pool table text.
- admin username placeholder was long/noisy.
- After:
- both user `My Requests` and admin Request Management are true paged views (10 rows/page) backed by API slicing.
- user top two sections are balanced and request pool gets full-width area below for cleaner readability.
- admin placeholder text is short and cleaner.
- lower payload per page improves perceived responsiveness under larger datasets.

### Follow-up UX hotfix (2026-04-19, same day)

Applied an additional layout correction based on feedback:

- `Submit a New Request` panel was made too wide in the previous pass.
- Desktop grid ratio is now intentionally asymmetric:
- left (`Submit a New Request`) narrower
- right (`My Requests`) wider

Files adjusted:

- `jellyfin-web/src/apps/stable/routes/request/index.tsx`
- `jellyfin-web/src/components/contentRequests/RequestList.tsx`
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`

Result:

- `My Requests` gets more horizontal room again, reducing table text wrapping pressure.
- `Submit a New Request` no longer occupies equal-width space.

### Follow-up UX tweak (2026-04-19, same day, latest)

Applied one more narrow-scope layout adjustment:

- Desktop top-grid columns were set back to equal width for:
- `Submit a New Request`
- `My Requests`

No other sizing/spacing/length changes were made in this tweak.

### Follow-up UX tweak (2026-04-19, My Requests readability)

Applied a focused readability update for the user `My Requests` view:

- Removed `Request ID` from `My Requests` table.
- Reduced `Season` column width (optimized for small numeric values).
- Increased `Title` column space.
- Switched title rendering from single-line truncation to multi-line wrapping.
- Title wrapping now prefers normal word boundaries (space-aware wrapping).

Files updated:

- `jellyfin-web/src/components/contentRequests/RequestList.tsx`
- `jellyfin-web/src/components/contentRequests/contentRequests.scss`

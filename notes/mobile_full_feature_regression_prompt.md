# Reusable Prompt: Real Android Full UI Regression + Crash Fix (Source-Aware)

Use this prompt whenever you want Codex to validate full Android app UI behavior (not only Subscription), fix real crashes/regressions, and retest.

```text
Run full Android APP Functional + UI regression on real device, analyze source-first, fix real crashes/regressions, and retest until stable.

Environment:
- Workspace root: C:\Users\Barai Brothers\Documents\Jellyfin
- Server target: http://127.0.0.1:8097/web
- Real device target: 100.114.21.113:5555
- APK project: C:\Users\Barai Brothers\Documents\Jellyfin\jellyfin-android
- Android package: org.jellyfin.mobile.debug

Users:
- Active user: Rohan / prnrr123
- Expired user: Test / Pass
- Admin user: baraibrothers / prnrr123

Mandatory execution order:
1) Build + install + launch app:
- Build Android APK: gradlew assembleDebug
- Install proprietary debug APK with adb install -r
- Launch package before tests.

2) Source-code analysis before UI actions:
- Parse route definitions from:
  - jellyfin-web/src/apps/stable/routes/asyncRoutes/user.ts
  - jellyfin-web/src/apps/stable/routes/legacyRoutes/user.ts
  - jellyfin-web/src/apps/stable/routes/asyncRoutes/public.ts
  - jellyfin-web/src/apps/stable/routes/legacyRoutes/public.ts
  - jellyfin-web/src/apps/stable/routes/routes.tsx
- Build route coverage list:
  - safe static routes
  - role-gated routes (admin-only / subscription-gated)
  - parameterized routes (test only when valid params discovered at runtime)

3) Use real-device app WebView automation (not browser emulation):
- Detect WebView devtools socket from /proc/net/unix:
  webview_devtools_remote_<pid>
- adb forward tcp:9223 localabstract:webview_devtools_remote_<pid>
- attach via Playwright connectOverCDP(http://127.0.0.1:9223)

4) Full UI route sweep by role:
- Active user: home/media/search/profile/settings/subscription/quickconnect + discovered runtime links.
- Expired user: same route intents but validate redirect/gating behavior (subscription required) without crash.
- Admin user: full user routes + dashboard/metadata/admin surfaces + discovered runtime links.
- Discover additional links dynamically from nav drawer + user menu + profile/settings pages and include them.

5) Crash/regression detection rules:
- FAIL conditions:
  - WebView/app process dies (pid missing / page target closed repeatedly)
  - uncaught runtime errors causing unusable screen
  - persistent blank page after retry
  - broken navigation loop/stuck loading
- PASS conditions that should NOT be false-failed:
  - expected permission redirect (e.g., expired -> subscription)
  - expected empty-state pages (“0-0 of 0”, “Nothing here”)
  - admin-only route redirect for non-admin without crash

6) Reliability handling:
- Login submit in WebView must use .manualLoginForm.requestSubmit().
- If CDP target closes, reconnect and retry scenario once.
- If a page is blank once, reload that route once before failing.
- Verify app process still alive after each scenario:
  adb shell pidof org.jellyfin.mobile.debug

7) Fix workflow (real issues only):
- Patch source for confirmed crashes/regressions.
- Rebuild/redeploy/reinstall as needed.
- Retest only failed scenarios first, then full pass.
- Keep mobile behavior intact unless fixing a real bug.

8) Artifacts to save:
- .run/ui-test/real-android/real_android_full_ui_regression_report.json
- .run/ui-test/real-android/real_android_full_ui_regression_report.md
- route-level fail screenshots in .run/ui-test/real-android/
- include per-user route counts, failed routes, console/page errors, and final URLs.

9) Final response format:
- Coverage summary by user (Active / Expired / Admin)
- Confirmed issues found
- Fixes applied (file paths)
- Retest result (pass/fail)
- Residual risks / follow-up checks

Do not stop at Subscription-only checks. Must cover full reachable Android app UI.
```

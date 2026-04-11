# Reusable Prompt: Change Default Hardcoded Server URL

Copy/paste this prompt when you want the default URL changed to a new domain.

```text
Task: Update the default hardcoded server URL across Jellyfin clients.

New default URL:
{{NEW_DEFAULT_URL}}

Constraints:
1) Keep existing hardcoded-mode behavior exactly as-is.
2) Only change the default fallback URL string.
3) Preserve precedence order:
   - Android + AndroidTV: Gradle property `jellyfin.server.url` -> env `SERVER_URL` -> default URL
   - Web: env `JELLYFIN_SERVER_URL` -> env `SERVER_URL` -> default URL
4) Do not modify authentication, subscription/expiry logic, middleware, routes, or API contracts.
5) Do not modify repos outside:
   - jellyfin-android
   - jellyfin-androidtv
   - jellyfin-web

Files to update:
1) jellyfin-android/app/build.gradle.kts
   - defaultConfig `serverUrl` fallback default string
2) jellyfin-androidtv/app/build.gradle.kts
   - defaultConfig `serverUrl` fallback default string
3) jellyfin-web/webpack.common.js
   - `HARDCODED_SERVER_URL` fallback default string

Validation (mandatory):
1) In `jellyfin-android`: `./gradlew.bat assembleDebug`
2) In `jellyfin-androidtv`: `./gradlew.bat assembleDebug`
3) In `jellyfin-web`: `npm run build:production`

Deliverables:
1) Exact modified file list
2) Build results for all 3 targets
3) Confirmation that only default URL values were changed
```

Example value:
`{{NEW_DEFAULT_URL}} = https://media.example.com/`

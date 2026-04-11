## Prompt: Replace Request Tab Subscription Banner GIF

You are a senior frontend engineer working on Jellyfin Web (React + SCSS).
Replace the GIF used in the Request tab subscription-state banner icon.

### Current implementation
- Component: `jellyfin-web/src/components/contentRequests/RequestQuotaSummary.tsx`
- Current GIF import:
  - `assets/branding/request-subscription-inactive-v2.gif`
- Icon rendering helper:
  - `SubscriptionStateIcon`
- The same icon is used in `grace`, `expired`, and `inactive` subscription banners.
- CSS sizing/alignment:
  - `jellyfin-web/src/components/contentRequests/contentRequests.scss`
  - classes: `.requestStateBannerIcon`, `.requestStateBannerIconMedia`, `.requestStateBannerIconImage`
  - icon must remain `20x20`, centered, non-stretching.

### Task
1. Replace the current GIF with a new one provided at a local path (I will give you the path).
2. Copy that file into:
   - `jellyfin-web/src/assets/branding/`
3. Use a **new filename** (for cache-busting), e.g. `request-subscription-inactive-v3.gif`.
4. Update `RequestQuotaSummary.tsx` to import and use the new filename.
5. Keep the icon decorative:
   - `aria-hidden="true"` on wrapper
   - `alt=""` on `<img>`
6. Do not change business logic.

### Constraints
- Keep the existing responsive layout intact on desktop and mobile.
- Keep icon dimensions fixed to `20x20`.
- Do not introduce PNG/static fallback unless explicitly requested.

### Verify
1. Build web:
   - `npm -C jellyfin-web run build:production`
2. Rebuild server:
   - `dotnet build jellyfin/Jellyfin.Server/Jellyfin.Server.csproj -c Debug`
3. Restart 8097 instance:
   - stop current process on port `8097`
   - run `.run/start_jf_source_8097_exe.bat`
4. Health check:
   - `http://127.0.0.1:8097/System/Info/Public`
5. Runtime check in browser:
   - Request tab loads icon from the **new GIF filename** under `/web/assets/branding/...`

### Output format
- List exact files changed.
- Confirm deployed PID on port `8097`.
- Confirm health-check success.
- Confirm final GIF URL being used in runtime.

# Prompt: Swap Request System GIFs (Badge + Popup Accent)

Use this prompt whenever you want to replace only the two Request System GIF assets.

## Prompt Text

You are working in `C:\Users\Barai Brothers\Documents\Jellyfin`.

Target repo: `jellyfin-web` only.

Task: replace the two Request System GIF assets used by admin request badge + request notification popup.

### New source GIFs (local machine)

- Admin badge GIF source: `<PUT_ABSOLUTE_PATH_TO_NEW_ADMIN_BADGE_GIF>`
- Popup accent GIF source: `<PUT_ABSOLUTE_PATH_TO_NEW_POPUP_ACCENT_GIF>`

### Replace these files exactly

- `jellyfin-web/src/assets/branding/admin-request-badge.gif`
- `jellyfin-web/src/assets/branding/request-popup-accent.gif`

### Rules

- Do not change any TS/JS/SCSS logic.
- Do not change import paths or asset filenames.
- Do not modify server/backend code.
- Do not modify Android/AndroidTV/Desktop code.
- Keep scope strictly to the 2 GIF files.

### Validation

Inside `jellyfin-web` run:

1. `npm run build:check`
2. `npm run build:production`

Scope checks:

1. `rg "admin-request-badge\\.gif|request-popup-accent\\.gif" src -n`
2. `git status --short`

Runtime path checks after restart/deploy:

- `http://localhost:8097/web/assets/branding/admin-request-badge.gif`
- `http://localhost:8097/web/assets/branding/request-popup-accent.gif`

### Deliverables

- List of changed files (should be only the 2 GIF files).
- Build result summary.
- Confirmation that no other files were changed.

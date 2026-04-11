# Prompt: Swap Dual Loader GIFs (System + Media)

Use this prompt when you want to replace only the two loader GIF files without changing loader logic.

## Prompt Text

You are working in `C:\Users\Barai Brothers\Documents\Jellyfin`.

Target repo: `jellyfin-web` only.

Task: replace the two loader GIF assets used by the dual loader system.

### New source GIFs (local machine)
- System loader source: `<PUT_ABSOLUTE_PATH_TO_NEW_SYSTEM_GIF>`
- Media loader source: `<PUT_ABSOLUTE_PATH_TO_NEW_MEDIA_GIF>`

### Replace these files exactly
- `jellyfin-web/src/assets/branding/system-loader.gif`
- `jellyfin-web/src/assets/branding/media-loader.gif`

### Rules
- Do not change any TypeScript/JS/SCSS logic.
- Do not change loader paths in code.
- Do not modify backend/server/native Android code.
- Keep existing filenames unchanged.

### Validation
Inside `jellyfin-web` run:
1. `npm run build:check`
2. `npm run build:production`

Then verify these URLs return `200` after server restart/deploy:
- `http://localhost:8097/web/assets/branding/system-loader.gif`
- `http://localhost:8097/web/assets/branding/media-loader.gif`

### Deliverables
- List of changed files (should be only the 2 GIF files).
- Build result summary.
- Confirmation that no other files were changed.


# Prompt: Reset Loader System Back to Default Spinner

Use this prompt when you want to fully remove the dual GIF loader implementation and restore default loader behavior.

## Prompt Text

You are working in `C:\Users\Barai Brothers\Documents\Jellyfin`.

Target repo: `jellyfin-web` only.

Task: fully reset all dual-loader changes and restore the original default spinner loader implementation.

### Required reset scope

1. Loader core
- Restore `src/components/loading/loading.ts` to default spinner-based implementation:
  - `show()` has no parameters.
  - `hide()` unchanged signature.
  - spinner DOM structure is restored (no GIF image element).
  - no `LoaderType`, no system/media switching.

2. Loader styles
- Restore `src/components/loading/loading.scss` to default spinner styles:
  - `.mdl-spinner`, `.mdlSpinnerActive`, spinner keyframes and related classes.
  - remove `.gif-loader` / `.loaderImage` styles.

3. Global typing
- Restore `src/global.d.ts`:
  - `window.Loading.show();`
  - `window.Loading.hide();`

4. Call sites
- Convert all `loading.show('system')` back to `loading.show()` in the previously modified system-flow files.

5. Assets cleanup
- Remove:
  - `src/assets/branding/system-loader.gif`
  - `src/assets/branding/media-loader.gif`

6. No extra scope
- Do not change backend code.
- Do not change Android native code.
- Do not modify unrelated frontend features.

### Validation
Inside `jellyfin-web` run:
1. `npm run build:check`
2. `npm run build:production`
3. `npx stylelint "src/components/loading/loading.scss"`

Scope checks:
1. `rg "loading\\.show\\('system'\\)" src -n` should return no results.
2. `rg "system-loader\\.gif|media-loader\\.gif" src -n` should return no results.

### Deliverables
- Modified file list.
- Build/style check results.
- Confirmation that loader API is back to default and GIF assets are removed.


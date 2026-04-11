# Post-Deploy Sanity Rule (8097)

Run this after every build + deploy before handing off for testing.

## Required checks
1. Server health:
`Invoke-WebRequest -UseBasicParsing http://127.0.0.1:8097/System/Info/Public`
2. Account login sanity + subscription/grace/access checks:
`powershell -ExecutionPolicy Bypass -File .run/scripts/post-deploy-sanity-8097.ps1`
3. Web smoke check:
1. Open `http://127.0.0.1:8097/web/#/login`
2. Validate:
1. `Test` should route to `/subscription` when fully expired.
2. `Grace` should route to `/home` and stay allowed.
3. Tail server log for new errors:
`Get-Content .run/jf-8097/logs/log_20260223.log -Tail 120`

## Mobile/TV rule
Use virtual devices for test verification:
1. Android mobile emulator (phone + tablet)
2. Android TV emulator

## Notes
1. Current account labels vs data may drift; this script reports mismatch without blocking unless auth itself fails.
2. If you see repeated `"Token is required" /ws` or `Invalid token`, clear saved browser session data and retry.

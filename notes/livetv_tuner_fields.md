# Live TV tuner fields (Jellyfin Dashboard)

Flow verified on: 2026-02-20
URL: http://127.0.0.1:8097/web/#/dashboard/livetv/tuner
Login user: baraibrothers

## Available tuner types
- HD Homerun (`hdhomerun`)
- M3U Tuner (`m3u`)
- Other (`other`)

## Field requirements by tuner type

### HD Homerun
Visible fields:
- Tuner IP Address (text) - optional
- Fallback max stream bitrate (Mbps) (number, default `30`) - required

Visible toggles:
- Restrict to channels marked as favorite
- Allow hardware transcoding

Behavior:
- Save button is available
- Detect My Devices button is available
- DRM note shown: "Channels with DRM will not be imported."

### M3U Tuner
Visible fields:
- File or URL (text) - required
- User agent (text) - optional
- Simultaneous stream limit (number, default `0`) - required
- Fallback max stream bitrate (Mbps) (number, default `30`) - required

Visible toggles:
- Allow fMP4 transcoding container
- Allow stream sharing (default ON)
- Auto-loop live streams
- Ignore DTS (decoding timestamp) (default ON)
- Read input at native frame rate (default ON)

Behavior:
- Save button is available
- Detect My Devices button is available

### Other
Visible fields:
- None

Behavior:
- Save button is hidden/disabled for this type
- Detect My Devices button is available
- If no devices are found: "No new devices found. To add a new tuner, close this dialog and enter the device information manually."

## Source references
- jellyfin-web/src/apps/dashboard/controllers/livetvtuner.js
- jellyfin-web/src/apps/dashboard/controllers/livetvtuner.html

# Reusable Prompt: Tailscale ADB + scrcpy (Screen Off)

Use this prompt in Codex whenever you want to set up Android control over Tailscale:

```text
I have connected my Android phone by USB for first-time pairing.

Set up Android debugging/control end-to-end with these exact goals:

1) Ensure tools
- Check if `adb` is installed; install if missing.
- Check if `scrcpy` is installed; install if missing.

2) USB + wireless debugging bootstrap
- Verify USB device is visible in `adb devices -l`.
- Switch device to TCP mode on port 5555 (`adb -d tcpip 5555`).

3) Connect over Tailscale (off local network)
- Connect ADB to: `100.114.21.113:5555`
- Use `adb disconnect` first, then connect to this endpoint.
- Verify device is `device` state via `adb -s 100.114.21.113:5555 get-state`.

4) Device settings and unlock
- Run and verify this command:
  `adb -s 100.114.21.113:5555 shell settings put secure vivo_secure_input_method 0`
  then:
  `adb -s 100.114.21.113:5555 shell settings get secure vivo_secure_input_method`
- Wake/unlock phone via ADB keyevents if needed.
- Set stay-awake policy:
  `adb -s 100.114.21.113:5555 shell settings put global stay_on_while_plugged_in 3`

5) Start scrcpy for remote use with phone display off
- Launch:
  `scrcpy -s 100.114.21.113:5555 --stay-awake --turn-screen-off --power-off-on-close`
- Ensure scrcpy process is running.
- Ensure phone display is OFF (check with `dumpsys power`).

6) Final output
- Show:
  - connected adb device line
  - scrcpy PID
  - display state (OFF/ON)
  - any failure and exact fix applied

Do not stop halfway. Execute commands directly and keep going until everything is working.
```


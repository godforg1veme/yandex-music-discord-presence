# Yandex Music → Discord Presence: design

## Goal

Publish a free, open-source Windows utility that shows the track currently playing in the official Yandex Music desktop app as a Discord profile activity. A user downloads one executable, starts it, and sees the activity when both desktop apps are running.

## First release

- Windows 10/11 only.
- Official Yandex Music desktop app and desktop Discord client.
- No Yandex login, token, browser extension, installer, or separate settings window.
- One portable executable with a tray icon and an Exit action. It shows a short status or error in the tray menu.
- Track title, artist, and album art when a publicly reachable artwork URL can be resolved; otherwise a branded fallback image.
- A button opens the current track in Yandex Music when a reliable URL is available. The button is omitted otherwise.
- Presence changes on track and play-state changes. It is cleared on stop, after the player closes, and when the utility exits.
- The program never sends account credentials or playback history to a project server.

## Architecture

1. `MediaSource` watches Windows media sessions and selects the session belonging to the official Yandex Music desktop app. It emits normalized track, playback, timeline, and thumbnail updates.
2. `TrackResolver` derives a Yandex Music track URL and externally accessible cover URL when the media session exposes enough metadata. Failure is non-fatal: text presence still works.
3. `PresenceMapper` converts normalized state to Discord activity fields and avoids repeated identical updates. It bounds text lengths to Discord's limits.
4. `DiscordClient` connects to the local desktop Discord client through Rich Presence IPC, retries when Discord starts later, and clears the activity on exit.
5. `TrayHost` owns the process lifecycle, shows current connection status, and provides Exit.

The components communicate through a small `NowPlaying` model. Media selection, external URL resolution, and Discord transmission remain independent so each can be changed without rewriting the others.

## Display

The target is a native Discord activity card: Yandex Music name, cover, track, artist, and an open-track button where available. Discord controls the card layout. The Spotify-only Listen Along behavior is outside the first release. Timestamp and progress rendering must be verified against the actual desktop client before being advertised.

## Failure behavior

- If Discord is closed, the utility remains idle and connects when it appears.
- If Yandex Music is closed or paused, the activity is cleared.
- If the session reports incomplete metadata, no stale previous track is displayed.
- Network or artwork lookup failure falls back to text and a static image.
- If multiple media sessions exist, only the Yandex Music process is eligible.

## Verification

Manually verify in a second Discord account or with another viewer because activity buttons are not visible to their owner. Check launch order, track change, pause/resume, close/reopen of either app, multiple media apps, missing artwork, and a clean exit. Package the self-contained executable and document installation, privacy, and known limitations in the GitHub README.

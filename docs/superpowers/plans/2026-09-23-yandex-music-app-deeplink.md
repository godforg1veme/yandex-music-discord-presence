# Open in Yandex Music Deep Link Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Discord activity publishing reliable while opening the uniquely matched track in the Yandex Music desktop app.

**Architecture:** Keep conservative Yandex search and ID validation. `TrackResolver` returns a Yandex HTTPS app-link and a browser fallback. `ActivityMapper` carries the URL and Windows playback timestamp; `DiscordIpcClient` omits the button when there is no URL and reports RPC errors. Discord receives HTTPS, while the app-link can hand off to the registered desktop app.

**Tech Stack:** .NET 10 Windows Forms application, Discord Rich Presence IPC, existing xUnit test project.

**Spec:** `docs/superpowers/specs/2026-09-23-yandex-music-app-deeplink-design.md`

## Global Constraints

- Windows 10/11 only; official Yandex Music desktop app and desktop Discord client.
- No Yandex login, token, browser extension, installer, or separate settings window.
- One portable executable with a tray icon and an Exit action.
- No account credentials or playback history sent to a project server.
- Clear presence on pause, stop, player close, and utility exit.
- Show the button only when the search resolves exactly one valid album and track ID.

---

## File Map

- `src/YandexMusicDiscord/TrackResolver.cs`: match multiple artists, return an HTTPS app-link and browser fallback, retry unsuccessful searches.
- `src/YandexMusicDiscord/DiscordIpcClient.cs`: label the button «В Яндекс Музыке», omit it without a URL, and send a playback timestamp.
- `src/YandexMusicDiscord/WindowsMediaSource.cs` and `ActivityCoordinator.cs`: carry playback timing and retry with a browser link if Discord rejects the app-link.
- `tests/YandexMusicDiscord.Tests/ActivityTests.cs` and `PresenceFlowTests.cs`: check link encoding and the complete local media-search-IPC flow.
- `README.md`: describe the button as linking to the current track in Yandex Music.
- `docs/development.md` and `docs/releases/v0.3.0.md`: document the handoff, fallbacks, time sync, and release.

## Task 1: Link to the current track from Discord Rich Presence

**Files:**
- Modify: `src/YandexMusicDiscord/TrackResolver.cs`
- Modify: `src/YandexMusicDiscord/DiscordIpcClient.cs`
- Modify: `tests/YandexMusicDiscord.Tests/ActivityTests.cs`
- Modify: `README.md`
- Modify: `docs/development.md`

**Interfaces:**
- `TrackResolver.ResolveResult(NowPlaying, JsonElement)` returns `(CoverUrl, TrackUrl, BrowserTrackUrl)`.
- `ActivityMapper.Map(NowPlaying?, string?, string?, string?)` carries both URLs and the playback start timestamp.
- Discord IPC sends zero or one button; its label is «В Яндекс Музыке» (28 UTF-8 bytes). Without a URL, the `buttons` field is omitted.

- [x] **Step 1: Return a supported HTTPS app-link for a validated search result.** In `TrackResolver.ResolveResult`, retain the existing exact title/artist matching, ambiguity rejection, album existence checks, and numeric ID validation. URL-encode `yandexmusic://album/{albumId}/track/{trackId}` into `deeplink_url` on the `https://music.app.link/` URL.

  Keep `(null, null, null)` for every result that fails validation so `ActivityMapper` omits the button when no reliable track URL exists.

- [x] **Step 2: Keep the Discord button label within its byte limit.** In `DiscordIpcClient.SetActivityAsync`, include the one-button array only when the URL exists and use «В Яндекс Музыке». The resolver expectation checks the encoded app-link; an additional assertion ensures the label is at most 31 UTF-8 bytes.

  Assert the encoded `music.app.link` URL and that a failed lookup still publishes an activity without `buttons`.

- [x] **Step 3: Update the user-facing explanations.** Describe the HTTPS app-link handoff and preserve the existing explanation that ambiguous matches or lookup failures omit the button while text presence works.

- [x] **Step 4: Compile, package, and install the application.** Release build and publish succeeded; the installed EXE SHA-256 matched the published EXE. The previous executable was saved as a backup. End-to-end button clicking remains for manual verification from a second Discord account, because Discord hides Rich Presence buttons from their owner.

- [x] **Step 5: Fix publication, artwork, and playback timing.** Omit `buttons` without a URL, match tracks with multiple artists, retry failed lookups, and derive the Discord start timestamp from the Windows media timeline. Add local end-to-end tests with a fake media source, HTTP response, and Discord IPC pipe; run the test suite and package v0.3.0 with release notes.

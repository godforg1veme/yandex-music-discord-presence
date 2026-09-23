# Yandex Music Discord Presence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portable Windows utility that mirrors the official Yandex Music desktop app's current track into Discord Rich Presence.

**Architecture:** A Windows media-session adapter selects Yandex Music, a mapper produces stable activity payloads, and a Discord IPC client sends them to the desktop client. A tray host owns polling, reconnection, and shutdown.

**Tech Stack:** .NET 10, Windows Forms tray UI, Windows.Media.Control, System.IO.Pipes, System.Text.Json, xUnit for isolated model and protocol tests.

**Spec:** `docs/superpowers/specs/2026-09-23-yandex-music-discord-presence-design.md`

## Global Constraints

- Windows 10/11 only; official Yandex Music desktop app and desktop Discord client.
- No Yandex login, token, browser extension, installer, or separate settings window.
- One portable executable with tray icon and Exit action.
- No account credentials or playback history sent to a project server.
- Clear presence on pause, stop, player close, and utility exit.

## File Map

- `src/YandexMusicPresence/YandexMusicPresence.csproj`: Windows executable and publish settings.
- `src/YandexMusicPresence/Program.cs`: single-instance startup and tray lifecycle.
- `src/YandexMusicPresence/NowPlaying.cs`: normalized immutable playback state.
- `src/YandexMusicPresence/WindowsMediaSource.cs`: media-session selection and snapshot reads.
- `src/YandexMusicPresence/DiscordIpcClient.cs`: framed Discord IPC and reconnect.
- `src/YandexMusicPresence/PresenceMapper.cs`: activity payload and change detection.
- `src/YandexMusicPresence/PresenceCoordinator.cs`: poll loop, status, and clear behavior.
- `src/YandexMusicPresence/TrackResolver.cs`: optional public cover and track URL resolution; conservative fallback.
- `tests/YandexMusicPresence.Tests/`: unit tests for selection, mapping, and framing.
- `README.md`: installation, use, privacy, limitations, and release build.

---

### Task 1: Foundation and model

**Files:** Create `src/YandexMusicPresence/YandexMusicPresence.csproj`, `NowPlaying.cs`, `Program.cs`, `tests/YandexMusicPresence.Tests/YandexMusicPresence.Tests.csproj`, `tests/YandexMusicPresence.Tests/PresenceMapperTests.cs`.

**Interfaces:** `NowPlaying(string Title, string Artist, string? Album, bool IsPlaying, TimeSpan Position, TimeSpan Duration, string SourceId)`; `PresenceMapper.Map(NowPlaying track, string? coverUrl, string? trackUrl) -> DiscordActivity`.

- [ ] **Step 1:** Create Windows Forms project targeting `net10.0-windows10.0.19041.0`, with Windows targeting and single-file publish enabled. Create test project and project references.
- [ ] **Step 2:** Add a model test: a paused track maps to no activity; an active track maps title and artist. Run `dotnet test` and confirm the missing mapper fails compilation.
- [ ] **Step 3:** Implement immutable models and mapper with Discord field length limits (details and state 128 characters). Run `dotnet test` until green.
- [ ] **Step 4:** Commit foundation and tests.

### Task 2: Read only the Yandex Music media session

**Files:** Create `WindowsMediaSource.cs`; add `MediaSourceTests.cs`.

**Interfaces:** `WindowsMediaSource.ReadAsync(CancellationToken) -> Task<NowPlaying?>`; `WindowsMediaSource.IsYandexMusicSource(string sourceId) -> bool`.

- [ ] **Step 1:** Test source matching against Yandex Music desktop process IDs and rejection of browsers, Spotify, and empty IDs.
- [ ] **Step 2:** Implement session enumeration with `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, `GetSessions()`, `SourceAppUserModelId`, `TryGetMediaPropertiesAsync()`, `GetPlaybackInfo()`, and `GetTimelineProperties()`. Never select another app as a fallback.
- [ ] **Step 3:** Test build and run a diagnostic invocation against the installed Yandex Music app if present; record any observed source ID in tests or documentation.
- [ ] **Step 4:** Commit media source.

### Task 3: Discord transport and presence lifecycle

**Files:** Create `DiscordIpcClient.cs`, `PresenceCoordinator.cs`, `tests/YandexMusicPresence.Tests/DiscordIpcTests.cs`.

**Interfaces:** `DiscordIpcClient.SetActivityAsync(DiscordActivity?, CancellationToken) -> Task<bool>`; `PresenceCoordinator.RunAsync(CancellationToken) -> Task`.

- [ ] **Step 1:** Test the Discord IPC frame: little-endian opcode and payload length followed by UTF-8 JSON. Test that repeated identical tracks do not enqueue repeated updates.
- [ ] **Step 2:** Implement named-pipe connection (`discord-ipc-0` to `discord-ipc-9`), handshake, `SET_ACTIVITY`, reconnect, and explicit clear payload. Reject oversized or malformed incoming frames.
- [ ] **Step 3:** Implement coordinator polling with cancellation, activity deduplication, retry when Discord starts, and a final clear on shutdown.
- [ ] **Step 4:** Run unit tests and commit transport.

### Task 4: Artwork, links, and tray packaging

**Files:** Create `TrackResolver.cs`; complete `Program.cs`; create `README.md`, `.gitignore`, and app icon resources if available.

**Interfaces:** `TrackResolver.ResolveAsync(NowPlaying track, CancellationToken) -> Task<(string? CoverUrl, string? TrackUrl)>`.

- [ ] **Step 1:** Add focused tests for ambiguous search results and network failures: both must fall back to no URL rather than a wrong song.
- [ ] **Step 2:** Implement only a resolver that can validate a reliable public track/cover URL. If Windows media metadata has no stable ID and the lookup cannot validate an exact match, use a bundled Discord application art asset and omit the button.
- [ ] **Step 3:** Add tray state text, Exit action, cancellation, and single-instance guard. The application must remain usable if Discord or Yandex Music starts later.
- [ ] **Step 4:** Build and publish `win-x64` self-contained, single-file executable; run smoke checks for launch, idle, and clean exit. Document manual two-account Discord verification and limitations.
- [ ] **Step 5:** Commit release-ready source and documentation.

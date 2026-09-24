namespace YandexMusicDiscord;

public sealed record NowPlaying(
    string Title,
    string Artist,
    string? Album,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    string SourceId,
    DateTimeOffset? PositionUpdatedAt = null);

public sealed record DiscordActivity(
    string Details,
    string State,
    string LargeImage,
    string? TrackUrl,
    string? BrowserTrackUrl = null,
    long? StartedAtUnixSeconds = null);

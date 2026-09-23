namespace YandexMusicPresence;

public sealed record NowPlaying(
    string Title,
    string Artist,
    string? Album,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    string SourceId);

public sealed record DiscordActivity(
    string Details,
    string State,
    string LargeImage,
    string? TrackUrl);

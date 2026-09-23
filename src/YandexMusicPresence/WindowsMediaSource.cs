using Windows.Media.Control;

namespace YandexMusicPresence;

public sealed class WindowsMediaSource
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public static bool IsYandexMusicSource(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return false;
        var normalized = sourceId.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace(".", "").Replace("-", "");
        return normalized.Contains("yandexmusic", StringComparison.Ordinal) ||
               normalized.Contains("яндексмузыка", StringComparison.Ordinal);
    }

    public async Task<NowPlaying?> ReadAsync(CancellationToken cancellationToken)
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        var session = _manager.GetSessions().FirstOrDefault(s => IsYandexMusicSource(s.SourceAppUserModelId));
        if (session is null) return null;

        var playback = session.GetPlaybackInfo();
        if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) return null;

        var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        if (properties is null || string.IsNullOrWhiteSpace(properties.Title) || string.IsNullOrWhiteSpace(properties.Artist))
            return null;

        var timeline = session.GetTimelineProperties();
        return new NowPlaying(
            properties.Title,
            properties.Artist,
            properties.AlbumTitle,
            true,
            timeline.Position,
            timeline.EndTime - timeline.StartTime,
            session.SourceAppUserModelId);
    }
}

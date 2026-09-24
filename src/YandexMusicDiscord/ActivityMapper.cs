namespace YandexMusicDiscord;

public static class ActivityMapper
{
    private const string FallbackArtworkKey = "yandex-music";
    public static DiscordActivity? Map(
        NowPlaying? track,
        string? coverUrl = null,
        string? trackUrl = null,
        string? browserTrackUrl = null)
    {
        if (track is null || !track.IsPlaying ||
            string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist))
            return null;

        return new DiscordActivity(
            Bound(track.Title.Trim()),
            Bound(track.Artist.Trim()),
            string.IsNullOrWhiteSpace(coverUrl) ? FallbackArtworkKey : coverUrl,
            string.IsNullOrWhiteSpace(trackUrl) ? null : trackUrl,
            string.IsNullOrWhiteSpace(browserTrackUrl) ? null : browserTrackUrl,
            PlaybackStartedAt(track));
    }

    private static long? PlaybackStartedAt(NowPlaying track)
    {
        if (track.Position < TimeSpan.Zero) return null;
        var now = DateTimeOffset.UtcNow;
        var updatedAt = track.PositionUpdatedAt is { } timestamp &&
            timestamp >= DateTimeOffset.UnixEpoch && timestamp <= now.AddSeconds(5)
                ? timestamp
                : now;
        try { return (updatedAt - track.Position).ToUnixTimeSeconds(); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string Bound(string value) => value.Length <= 128 ? value : value[..127] + "…";
}

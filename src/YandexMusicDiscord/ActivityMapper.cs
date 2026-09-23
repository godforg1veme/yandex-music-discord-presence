namespace YandexMusicDiscord;

public static class ActivityMapper
{
    public static DiscordActivity? Map(NowPlaying? track, string? coverUrl = null, string? trackUrl = null)
    {
        if (track is null || !track.IsPlaying ||
            string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist))
            return null;

        return new DiscordActivity(
            Bound(track.Title.Trim()),
            Bound(track.Artist.Trim()),
            string.IsNullOrWhiteSpace(coverUrl) ? "presence-icon" : coverUrl,
            string.IsNullOrWhiteSpace(trackUrl) ? null : trackUrl);
    }

    private static string Bound(string value) => value.Length <= 128 ? value : value[..127] + "…";
}

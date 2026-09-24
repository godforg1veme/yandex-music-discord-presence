namespace YandexMusicDiscord;

public interface INowPlayingSource
{
    Task<NowPlaying?> ReadAsync(CancellationToken cancellationToken);
}

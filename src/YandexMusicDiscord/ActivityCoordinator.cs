namespace YandexMusicDiscord;

public sealed class ActivityCoordinator
{
    private readonly INowPlayingSource _source;
    private readonly TrackResolver _resolver;
    private readonly DiscordIpcClient _discord;
    private DiscordActivity? _lastActivity;
    private DateTimeOffset _lastSent;

    public ActivityCoordinator(INowPlayingSource source, TrackResolver resolver, DiscordIpcClient discord)
    {
        _source = source;
        _resolver = resolver;
        _discord = discord;
    }

    public string Status { get; private set; } = "Ожидание Яндекс Музыки";

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            var track = await _source.ReadAsync(cancellationToken);
            var links = track is null ? (CoverUrl: (string?)null, TrackUrl: (string?)null, BrowserTrackUrl: (string?)null) :
                await _resolver.ResolveAsync(track, cancellationToken);
            var activity = ActivityMapper.Map(track, links.CoverUrl, links.TrackUrl, links.BrowserTrackUrl);

            if (activity is not null && _lastActivity is not null &&
                activity.Details == _lastActivity.Details && activity.State == _lastActivity.State &&
                activity.StartedAtUnixSeconds is { } currentStart &&
                _lastActivity.StartedAtUnixSeconds is { } previousStart &&
                Math.Abs(currentStart - previousStart) <= 2)
                activity = activity with { StartedAtUnixSeconds = previousStart };

            if (activity is null)
            {
                if (_lastActivity is not null)
                    await _discord.SetActivityAsync(null, cancellationToken);
                _lastActivity = null;
                Status = "Ожидание Яндекс Музыки";
                return;
            }

            if (activity != _lastActivity || !_discord.IsConnected || DateTimeOffset.UtcNow - _lastSent > TimeSpan.FromSeconds(30))
            {
                if (await _discord.SetActivityAsync(activity, cancellationToken))
                {
                    _lastActivity = activity;
                    _lastSent = DateTimeOffset.UtcNow;
                    Status = $"В Discord: {activity.Details}";
                }
                else if (activity.BrowserTrackUrl is not null &&
                    await _discord.SetActivityAsync(activity with { TrackUrl = activity.BrowserTrackUrl, BrowserTrackUrl = null }, cancellationToken))
                {
                    _lastActivity = activity;
                    _lastSent = DateTimeOffset.UtcNow;
                    Status = $"В Discord: {activity.Details} (ссылка в браузер)";
                }
                else Status = _discord.LastError is { Length: > 0 } error ? $"Discord: {error}" : "Ожидание Discord";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Status = $"Ошибка: {ex.Message}";
        }
    }

    public async Task StopAsync()
    {
        if (_discord.IsConnected)
            await _discord.SetActivityAsync(null, CancellationToken.None);
        await _discord.DisposeAsync();
    }
}

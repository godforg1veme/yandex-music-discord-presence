using System.Text.Json;

namespace YandexMusicDiscord;

public sealed class TrackResolver
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;
    private string? _cachedKey;
    private (string? CoverUrl, string? TrackUrl, string? BrowserTrackUrl) _cachedValue;
    private DateTimeOffset _cachedAt;

    public TrackResolver(HttpClient? client = null, TimeProvider? timeProvider = null)
    {
        _client = client ?? SharedClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<(string? CoverUrl, string? TrackUrl, string? BrowserTrackUrl)> ResolveAsync(NowPlaying track, CancellationToken cancellationToken)
    {
        var key = $"{track.Title}\n{track.Artist}\n{track.Album}\n{track.Duration.Ticks}";
        if (key == _cachedKey &&
            (_cachedValue.CoverUrl is not null || _timeProvider.GetUtcNow() - _cachedAt < TimeSpan.FromSeconds(30)))
            return _cachedValue;

        (string? CoverUrl, string? TrackUrl, string? BrowserTrackUrl) resolved = (null, null, null);
        try
        {
            var firstArtist = track.Artist.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? track.Artist;
            var query = Uri.EscapeDataString($"{firstArtist} {track.Title}");
            using var response = await _client.GetAsync($"https://api.music.yandex.net/search?text={query}&page=0&type=all", cancellationToken);
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            resolved = ResolveResult(track, body.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // The search endpoint is optional; Discord still receives song and artist.
        }

        _cachedKey = key;
        _cachedValue = resolved;
        _cachedAt = _timeProvider.GetUtcNow();
        return resolved;
    }

    internal static (string? CoverUrl, string? TrackUrl, string? BrowserTrackUrl) ResolveResult(NowPlaying track, JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tracks", out var tracks) ||
            !tracks.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
            return (null, null, null);

        var matches = new List<JsonElement>();
        foreach (var item in items.EnumerateArray().Take(10))
        {
            if (!item.TryGetProperty("title", out var title) || !Same(title.GetString(), track.Title) ||
                !item.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
                continue;
            var artistNames = artists.EnumerateArray()
                .Where(artist => artist.ValueKind == JsonValueKind.Object &&
                    artist.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .Select(artist => artist.GetProperty("name").GetString())
                .ToArray();
            if (!Same(string.Join(", ", artistNames), track.Artist) &&
                !artistNames.Any(name => Same(name, track.Artist)))
                continue;
            matches.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(track.Album))
            matches = matches.Where(item => item.TryGetProperty("albums", out var albums) &&
                albums.ValueKind == JsonValueKind.Array && albums.EnumerateArray().Any(album =>
                    album.TryGetProperty("title", out var title) && Same(title.GetString(), track.Album))).ToList();

        if (matches.Count > 1 && track.Duration > TimeSpan.Zero)
        {
            matches = matches.Where(item => item.TryGetProperty("durationMs", out var duration) &&
                duration.TryGetInt64(out var milliseconds) &&
                Math.Abs(milliseconds - track.Duration.TotalMilliseconds) <= 2000).ToList();
        }
        if (matches.Count != 1) return (null, null, null);

        var match = matches[0];
        if (!match.TryGetProperty("id", out var id) || !match.TryGetProperty("albums", out var albumList) ||
            albumList.ValueKind != JsonValueKind.Array || albumList.GetArrayLength() == 0 ||
            !albumList[0].TryGetProperty("id", out var albumId)) return (null, null, null);

        var trackId = id.ToString();
        var albumNumber = albumId.ToString();
        if (!long.TryParse(trackId, out _) || !long.TryParse(albumNumber, out _)) return (null, null, null);
        string? coverUrl = null;
        if (match.TryGetProperty("coverUri", out var cover) && cover.ValueKind == JsonValueKind.String)
        {
            var coverPath = cover.GetString()?.Replace("%%", "400x400");
            if (coverPath?.StartsWith("avatars.yandex.net/", StringComparison.OrdinalIgnoreCase) == true)
                coverUrl = "https://" + coverPath;
        }
        // Keep the Discord button URL on HTTPS. Yandex's Branch link page hands
        // the encoded custom URI to its registered desktop-app handler.
        return (
            coverUrl,
            CreateDesktopAppLink(albumNumber, trackId),
            $"https://music.yandex.ru/album/{albumNumber}/track/{trackId}");
    }

    internal static string CreateDesktopAppLink(string albumId, string trackId)
    {
        var deepLink = Uri.EscapeDataString($"yandexmusic://album/{albumId}/track/{trackId}");
        return $"https://music.app.link/?actions=deeplink&deeplink_url={deepLink}";
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}

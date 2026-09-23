using System.Text.Json;

namespace YandexMusicPresence;

public sealed class TrackResolver
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _client;
    private string? _cachedKey;
    private (string? CoverUrl, string? TrackUrl) _cachedValue;

    public TrackResolver(HttpClient? client = null) => _client = client ?? SharedClient;

    public async Task<(string? CoverUrl, string? TrackUrl)> ResolveAsync(NowPlaying track, CancellationToken cancellationToken)
    {
        var key = $"{track.Title}\n{track.Artist}\n{track.Album}";
        if (key == _cachedKey) return _cachedValue;

        (string? CoverUrl, string? TrackUrl) resolved = (null, null);
        try
        {
            var query = Uri.EscapeDataString($"{track.Artist} {track.Title}");
            using var response = await _client.GetAsync($"https://api.music.yandex.net/search?text={query}&page=0&type=all", cancellationToken);
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            resolved = ResolveResult(track, body.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // The search endpoint is optional; Discord still receives song and artist.
        }

        _cachedKey = key;
        _cachedValue = resolved;
        return resolved;
    }

    internal static (string? CoverUrl, string? TrackUrl) ResolveResult(NowPlaying track, JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tracks", out var tracks) ||
            !tracks.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
            return (null, null);

        var matches = new List<JsonElement>();
        foreach (var item in items.EnumerateArray().Take(10))
        {
            if (!item.TryGetProperty("title", out var title) || !Same(title.GetString(), track.Title) ||
                !item.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
                continue;
            if (!artists.EnumerateArray().Any(artist =>
                artist.TryGetProperty("name", out var name) && Same(name.GetString(), track.Artist)))
                continue;
            matches.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(track.Album))
            matches = matches.Where(item => item.TryGetProperty("albums", out var albums) &&
                albums.ValueKind == JsonValueKind.Array && albums.EnumerateArray().Any(album =>
                    album.TryGetProperty("title", out var title) && Same(title.GetString(), track.Album))).ToList();
        if (matches.Count != 1) return (null, null);

        var match = matches[0];
        if (!match.TryGetProperty("id", out var id) || !match.TryGetProperty("albums", out var albumList) ||
            albumList.ValueKind != JsonValueKind.Array || albumList.GetArrayLength() == 0 ||
            !albumList[0].TryGetProperty("id", out var albumId)) return (null, null);

        var trackId = id.ToString();
        var albumNumber = albumId.ToString();
        if (!long.TryParse(trackId, out _) || !long.TryParse(albumNumber, out _)) return (null, null);
        string? coverUrl = null;
        if (match.TryGetProperty("coverUri", out var cover))
        {
            var coverPath = cover.GetString()?.Replace("%%", "400x400");
            if (coverPath?.StartsWith("avatars.yandex.net/", StringComparison.OrdinalIgnoreCase) == true)
                coverUrl = "https://" + coverPath;
        }
        return (coverUrl, $"https://music.yandex.ru/album/{albumNumber}/track/{trackId}");
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}

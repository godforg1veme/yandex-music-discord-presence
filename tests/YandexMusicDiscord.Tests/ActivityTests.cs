using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using YandexMusicDiscord;

namespace YandexMusicDiscord.Tests;

public class ActivityTests
{
    [Fact]
    public void PausedTrackDoesNotCreateActivity()
    {
        var track = new NowPlaying("Song", "Artist", null, false, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        Assert.Null(ActivityMapper.Map(track));
    }

    [Fact]
    public void ActiveTrackMapsTitleArtistAndArtwork()
    {
        var track = new NowPlaying("Song", "Artist", "Album", true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        var activity = ActivityMapper.Map(track, "https://example.com/cover.jpg", "https://music.app.link/test", "https://music.yandex.ru/album/1/track/2");
        Assert.Equal("Song", activity?.Details);
        Assert.Equal("Artist", activity?.State);
        Assert.Equal("https://example.com/cover.jpg", activity?.LargeImage);
        Assert.Equal("https://music.app.link/test", activity?.TrackUrl);
        Assert.Equal("https://music.yandex.ru/album/1/track/2", activity?.BrowserTrackUrl);
    }

    [Fact]
    public void MissingArtworkUsesUploadedDiscordAsset()
    {
        var track = new NowPlaying("Song", "Artist", null, true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        Assert.Equal("yandex-music", ActivityMapper.Map(track)?.LargeImage);
    }

    [Theory]
    [InlineData("\"C:\\Apps\\YandexMusicPresence.exe\"", true)]
    [InlineData("C:\\Apps\\YandexMusicPresence.exe", true)]
    [InlineData("\"C:\\Apps\\Other.exe\"", false)]
    [InlineData("\"C:\\Apps\\YandexMusicPresence.exe\" --flag", false)]
    public void RecognizesOnlyPreviousAutostartExecutable(string registryValue, bool expected)
    {
        Assert.Equal(expected, StartupRegistration.IsLegacyExecutableValue(registryValue));
    }

    [Theory]
    [InlineData("YandexMusic.exe", true)]
    [InlineData("ru.yandex.music", true)]
    [InlineData("ru.yandex.desktop.music", true)]
    [InlineData("A025C540.Yandex.Music_vfvw9svesycw6", true)]
    [InlineData("Яндекс Музыка", true)]
    [InlineData("chrome.exe", false)]
    [InlineData("Spotify.exe", false)]
    [InlineData("YandexBrowser.exe", false)]
    public void SelectsOnlyYandexMusic(string source, bool expected)
    {
        Assert.Equal(expected, WindowsMediaSource.IsYandexMusicSource(source));
    }

    [Fact]
    public void IpcFrameHasLittleEndianHeaderAndUtf8Body()
    {
        var frame = DiscordIpcClient.EncodeFrame(1, "{\"тест\":1}");
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(0, 4)));
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4, 4)));
        Assert.Equal("{\"тест\":1}", Encoding.UTF8.GetString(frame.AsSpan(8)));
    }

    [Fact]
    public void ResolverAcceptsOneExactResult()
    {
        using var doc = JsonDocument.Parse("""
            {"result":{"tracks":{"results":[{"id":12,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":34,"title":"Album"}],"coverUri":"avatars.yandex.net/get-music-content/example/%%"}]}}}
            """);
        var track = new NowPlaying("Song", "Artist", "Album", true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        var result = TrackResolver.ResolveResult(track, doc.RootElement);
        Assert.Equal("https://avatars.yandex.net/get-music-content/example/400x400", result.CoverUrl);
        Assert.Equal("https://music.app.link/?actions=deeplink&deeplink_url=yandexmusic%3A%2F%2Falbum%2F34%2Ftrack%2F12", result.TrackUrl);
        Assert.Equal("https://music.yandex.ru/album/34/track/12", result.BrowserTrackUrl);
    }

    [Fact]
    public void TrackButtonLabelFitsDiscordUtf8Limit()
    {
        Assert.True(Encoding.UTF8.GetByteCount(DiscordIpcClient.TrackButtonLabel) <= 31);
    }

    [Fact]
    public void ResolverRejectsAmbiguousSongs()
    {
        using var doc = JsonDocument.Parse("""
            {"result":{"tracks":{"results":[{"id":12,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":34}]},{"id":13,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":35}]}]}}}
            """);
        var track = new NowPlaying("Song", "Artist", null, true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        Assert.Equal((null, null, null), TrackResolver.ResolveResult(track, doc.RootElement));
    }

    [Fact]
    public async Task ResolverRetriesAFailedLookupForTheSameTrack()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var handler = new SequencedSearchHandler();
        using var http = new HttpClient(handler);
        var resolver = new TrackResolver(http, clock);
        var track = new NowPlaying("Song", "Artist", "Album", true,
            TimeSpan.Zero, TimeSpan.FromMinutes(3), "YandexMusic.exe");

        Assert.Null((await resolver.ResolveAsync(track, CancellationToken.None)).CoverUrl);
        Assert.Null((await resolver.ResolveAsync(track, CancellationToken.None)).CoverUrl);
        Assert.Equal(1, handler.Calls);

        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal("https://avatars.yandex.net/get-music-content/example/400x400",
            (await resolver.ResolveAsync(track, CancellationToken.None)).CoverUrl);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class SequencedSearchHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body = Calls == 1
                ? """{"result":{"tracks":{"results":[]}}}"""
                : """{"result":{"tracks":{"results":[{"id":12,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":34,"title":"Album"}],"coverUri":"avatars.yandex.net/get-music-content/example/%%"}]}}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

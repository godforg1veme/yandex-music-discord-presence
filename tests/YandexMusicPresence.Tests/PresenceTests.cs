using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Xunit;
using YandexMusicPresence;

namespace YandexMusicPresence.Tests;

public class PresenceTests
{
    [Fact]
    public void PausedTrackDoesNotCreateActivity()
    {
        var track = new NowPlaying("Song", "Artist", null, false, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        Assert.Null(PresenceMapper.Map(track));
    }

    [Fact]
    public void ActiveTrackMapsTitleArtistAndArtwork()
    {
        var track = new NowPlaying("Song", "Artist", "Album", true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        var activity = PresenceMapper.Map(track, "https://example.com/cover.jpg", "https://music.yandex.ru/album/1/track/2");
        Assert.Equal("Song", activity?.Details);
        Assert.Equal("Artist", activity?.State);
        Assert.Equal("https://example.com/cover.jpg", activity?.LargeImage);
        Assert.Equal("https://music.yandex.ru/album/1/track/2", activity?.TrackUrl);
    }

    [Theory]
    [InlineData("YandexMusic.exe", true)]
    [InlineData("ru.yandex.music", true)]
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
        Assert.Equal("https://music.yandex.ru/album/34/track/12", result.TrackUrl);
    }

    [Fact]
    public void ResolverRejectsAmbiguousSongs()
    {
        using var doc = JsonDocument.Parse("""
            {"result":{"tracks":{"results":[{"id":12,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":34}]},{"id":13,"title":"Song","artists":[{"name":"Artist"}],"albums":[{"id":35}]}]}}}
            """);
        var track = new NowPlaying("Song", "Artist", null, true, TimeSpan.Zero, TimeSpan.Zero, "YandexMusic.exe");
        Assert.Equal((null, null), TrackResolver.ResolveResult(track, doc.RootElement));
    }
}

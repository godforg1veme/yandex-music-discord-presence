using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using YandexMusicDiscord;

namespace YandexMusicDiscord.Tests;

public class PresenceFlowTests
{
    [Fact]
    public async Task MultiArtistTrackPublishesArtworkPositionAndButtonThenClearsOnPause()
    {
        const string searchResult = """
            {"result":{"tracks":{"results":[{"id":114775091,"title":"зима","artists":[{"name":"naomonis"},{"name":"DVRKLXGHT"},{"name":"iwilldiehere"}],"albums":[{"id":26273332,"title":"зима"}],"durationMs":255480,"coverUri":"avatars.yandex.net/get-music-content/example/%%"}]}}}
            """;
        var positionUpdatedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        var source = new StubSource(new NowPlaying(
            "зима", "naomonis, DVRKLXGHT, iwilldiehere", null, true,
            TimeSpan.FromSeconds(203), TimeSpan.FromSeconds(255),
            "ru.yandex.desktop.music", positionUpdatedAt));
        var handler = new StubSearchHandler(searchResult);
        using var http = new HttpClient(handler);
        var pipeName = "yandex-presence-flow-" + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var discord = new DiscordIpcClient("123456789012345678", [pipeName]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptHandshakeAsync(server, timeout.Token);

            using (var command = JsonDocument.Parse((await ReadFrameAsync(server, timeout.Token)).Payload))
            {
                var activity = command.RootElement.GetProperty("args").GetProperty("activity");
                Assert.Equal("зима", activity.GetProperty("details").GetString());
                Assert.Equal("naomonis, DVRKLXGHT, iwilldiehere", activity.GetProperty("state").GetString());
                Assert.Equal("https://avatars.yandex.net/get-music-content/example/400x400",
                    activity.GetProperty("assets").GetProperty("large_image").GetString());
                Assert.Equal((positionUpdatedAt - TimeSpan.FromSeconds(203)).ToUnixTimeSeconds(),
                    activity.GetProperty("timestamps").GetProperty("start").GetInt64());
                var button = Assert.Single(activity.GetProperty("buttons").EnumerateArray());
                Assert.Equal("В Яндекс Музыке", button.GetProperty("label").GetString());
                Assert.Equal(
                    "https://music.app.link/?actions=deeplink&deeplink_url=yandexmusic%3A%2F%2Falbum%2F26273332%2Ftrack%2F114775091",
                    button.GetProperty("url").GetString());
                await AcknowledgeAsync(server, command.RootElement, timeout.Token);
            }

            using var clear = JsonDocument.Parse((await ReadFrameAsync(server, timeout.Token)).Payload);
            Assert.Equal(JsonValueKind.Null, clear.RootElement.GetProperty("args").GetProperty("activity").ValueKind);
            await AcknowledgeAsync(server, clear.RootElement, timeout.Token);
        }, timeout.Token);

        var coordinator = new ActivityCoordinator(source, new TrackResolver(http), discord);
        await coordinator.TickAsync(timeout.Token);
        Assert.Equal("В Discord: зима", coordinator.Status);
        source.Current = null;
        await coordinator.TickAsync(timeout.Token);
        await serverTask;
        Assert.Equal("Ожидание Яндекс Музыки", coordinator.Status);
        Assert.Contains("naomonis зима", Uri.UnescapeDataString(handler.LastRequestUri?.Query ?? ""));
    }

    [Fact]
    public async Task MissingSearchMatchStillPublishesTextWithoutButtons()
    {
        var source = new StubSource(new NowPlaying(
            "Song", "Artist", null, true, TimeSpan.Zero, TimeSpan.FromMinutes(3), "ru.yandex.desktop.music"));
        using var http = new HttpClient(new StubSearchHandler("""{"result":{"tracks":{"results":[]}}}"""));
        var pipeName = "yandex-presence-flow-" + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var discord = new DiscordIpcClient("123456789012345678", [pipeName]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(timeout.Token);
            await AcceptHandshakeAsync(server, timeout.Token);
            using var command = JsonDocument.Parse((await ReadFrameAsync(server, timeout.Token)).Payload);
            var activity = command.RootElement.GetProperty("args").GetProperty("activity");
            Assert.Equal("Song", activity.GetProperty("details").GetString());
            Assert.Equal("yandex-music", activity.GetProperty("assets").GetProperty("large_image").GetString());
            Assert.False(activity.TryGetProperty("buttons", out _));
            await AcknowledgeAsync(server, command.RootElement, timeout.Token);
        }, timeout.Token);

        var coordinator = new ActivityCoordinator(source, new TrackResolver(http), discord);
        await coordinator.TickAsync(timeout.Token);
        await serverTask;
        Assert.Equal("В Discord: Song", coordinator.Status);
    }

    private static async Task AcceptHandshakeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var handshake = await ReadFrameAsync(stream, cancellationToken);
        Assert.Equal(0, handshake.Opcode);
        using var json = JsonDocument.Parse(handshake.Payload);
        Assert.Equal("123456789012345678", json.RootElement.GetProperty("client_id").GetString());
        await stream.WriteAsync(DiscordIpcClient.EncodeFrame(1, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\"}"), cancellationToken);
    }

    private static async Task AcknowledgeAsync(Stream stream, JsonElement command, CancellationToken cancellationToken)
    {
        var nonce = command.GetProperty("nonce").GetString();
        var reply = JsonSerializer.Serialize(new { cmd = "SET_ACTIVITY", nonce });
        await stream.WriteAsync(DiscordIpcClient.EncodeFrame(1, reply), cancellationToken);
    }

    private static async Task<(int Opcode, string Payload)> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return (BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4)), Encoding.UTF8.GetString(body));
    }

    private sealed class StubSource(NowPlaying? current) : INowPlayingSource
    {
        public NowPlaying? Current { get; set; } = current;

        public Task<NowPlaying?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
    }

    private sealed class StubSearchHandler(string response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}

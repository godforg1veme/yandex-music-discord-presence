using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Xunit;
using YandexMusicDiscord;

namespace YandexMusicDiscord.Tests;

public class DiscordIpcTests
{
    [Fact]
    public async Task SendsHandshakeAndListeningActivity()
    {
        var pipeName = "yandex-presence-test-" + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new DiscordIpcClient("123456789012345678", [pipeName]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(timeout.Token);
            var handshake = await ReadFrameAsync(server, timeout.Token);
            Assert.Equal(0, handshake.Opcode);
            using (var json = JsonDocument.Parse(handshake.Payload))
                Assert.Equal("123456789012345678", json.RootElement.GetProperty("client_id").GetString());

            await server.WriteAsync(DiscordIpcClient.EncodeFrame(1, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\"}"), timeout.Token);
            var update = await ReadFrameAsync(server, timeout.Token);
            Assert.Equal(1, update.Opcode);
            using var command = JsonDocument.Parse(update.Payload);
            Assert.Equal("SET_ACTIVITY", command.RootElement.GetProperty("cmd").GetString());
            Assert.Equal(2, command.RootElement.GetProperty("args").GetProperty("activity").GetProperty("type").GetInt32());
            Assert.Equal("Song", command.RootElement.GetProperty("args").GetProperty("activity").GetProperty("details").GetString());
            var nonce = command.RootElement.GetProperty("nonce").GetString();
            await server.WriteAsync(DiscordIpcClient.EncodeFrame(1, JsonSerializer.Serialize(new { cmd = "SET_ACTIVITY", nonce })), timeout.Token);
        }, timeout.Token);

        var sent = await client.SetActivityAsync(new DiscordActivity("Song", "Artist", "yandex_music", null), timeout.Token);
        await serverTask;
        Assert.True(sent);
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
}

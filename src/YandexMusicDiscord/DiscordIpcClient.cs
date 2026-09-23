using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace YandexMusicDiscord;

public sealed class DiscordIpcClient : IAsyncDisposable
{
    private readonly string _applicationId;
    private readonly IReadOnlyList<string> _pipeNames;
    private NamedPipeClientStream? _pipe;

    public DiscordIpcClient(string applicationId)
        : this(applicationId, Enumerable.Range(0, 10).Select(index => $"discord-ipc-{index}").ToArray()) { }

    internal DiscordIpcClient(string applicationId, IReadOnlyList<string> pipeNames)
    {
        _applicationId = applicationId;
        _pipeNames = pipeNames;
    }
    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task<bool> SetActivityAsync(DiscordActivity? activity, CancellationToken cancellationToken)
    {
        try
        {
            if (!await ConnectAsync(cancellationToken)) return false;
            var nonce = Guid.NewGuid().ToString("N");
            object? mapped = activity is null ? null : new
            {
                type = 2,
                details = activity.Details,
                state = activity.State,
                assets = new { large_image = activity.LargeImage, large_text = "Яндекс Музыка" },
                buttons = activity.TrackUrl is null ? null : new[] { new { label = "Открыть трек", url = activity.TrackUrl } }
            };
            var command = new { cmd = "SET_ACTIVITY", args = new { pid = Environment.ProcessId, activity = mapped }, nonce };
            await WriteFrameAsync(_pipe!, 1, JsonSerializer.Serialize(command), cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            while (true)
            {
                var frame = await ReadFrameAsync(_pipe!, timeout.Token);
                if (frame.Opcode == 3)
                {
                    await WriteFrameAsync(_pipe!, 4, frame.Payload, timeout.Token);
                    continue;
                }
                if (frame.Opcode != 1) return false;
                using var response = JsonDocument.Parse(frame.Payload);
                var root = response.RootElement;
                if (root.TryGetProperty("nonce", out var returned) && returned.GetString() == nonce)
                    return !(root.TryGetProperty("evt", out var evt) && evt.GetString() == "ERROR");
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException)
        {
            Disconnect();
            return false;
        }
    }

    private async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return true;
        Disconnect();
        foreach (var pipeName in _pipeNames)
        {
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(350));
                await pipe.ConnectAsync(timeout.Token);
                await WriteFrameAsync(pipe, 0, JsonSerializer.Serialize(new { v = 1, client_id = _applicationId }), timeout.Token);
                var ready = await ReadFrameAsync(pipe, timeout.Token);
                using var document = JsonDocument.Parse(ready.Payload);
                if (ready.Opcode == 1 && document.RootElement.TryGetProperty("evt", out var evt) && evt.GetString() == "READY")
                {
                    _pipe = pipe;
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException)
            {
                // Continue to the next Discord IPC endpoint.
            }
            pipe.Dispose();
        }
        return false;
    }

    internal static byte[] EncodeFrame(int opcode, string payload)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame.AsSpan(8));
        return frame;
    }

    private static async Task WriteFrameAsync(Stream stream, int opcode, string payload, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(EncodeFrame(opcode, payload), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<(int Opcode, string Payload)> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (length < 0 || length > 1024 * 1024) throw new IOException("Invalid Discord IPC frame length.");
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return (opcode, Encoding.UTF8.GetString(body));
    }

    private void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}

using System.Net;
using System.Text;
using WechatAIClient.Services;

namespace WechatAIClient.Tests;

internal sealed class RecordingHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Impl { get; set; }

    public int CallCount { get; private set; }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        Requests.Add(request);
        return Impl!(request, cancellationToken);
    }
}

internal sealed class SimpleFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public SimpleFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
}

internal sealed class MemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public Task SetSecretAsync(string key, string plaintext, CancellationToken cancellationToken = default)
    {
        _map[key] = plaintext;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_map.TryGetValue(key, out var v) ? v : null);

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        _map.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>Stream that emits <paramref name="prefix"/> then blocks until cancelled.</summary>
internal sealed class PrefixThenHangStream : Stream
{
    private readonly byte[] _prefix;
    private int _offset;
    private readonly CancellationToken _hangToken;

    public PrefixThenHangStream(string prefixUtf8, CancellationToken hangToken)
    {
        _prefix = Encoding.UTF8.GetBytes(prefixUtf8);
        _hangToken = hangToken;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset < _prefix.Length)
        {
            var n = Math.Min(buffer.Length, _prefix.Length - _offset);
            _prefix.AsSpan(_offset, n).CopyTo(buffer.Span);
            _offset += n;
            return n;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _hangToken);
        try
        {
            await Task.Delay(Timeout.Infinite, linked.Token);
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _hangToken.ThrowIfCancellationRequested();
            throw;
        }

        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal static class FakeHttpResponses
{
    public static HttpResponseMessage JsonStatus(HttpStatusCode code, string body = "{}")
        => new(code)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage Sse(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
        };

    public const string ChineseSse =
        "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"delta\":{\"content\":\"好\"}}]}\n\n" +
        "data: [DONE]\n\n";
}

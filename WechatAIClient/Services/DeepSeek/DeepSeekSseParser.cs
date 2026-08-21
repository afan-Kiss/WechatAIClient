using System.Text;
using System.Text.Json;
using WechatAIClient.Models;

namespace WechatAIClient.Services.DeepSeek;

/// <summary>
/// Pure SSE parser for DeepSeek/OpenAI-style chat completion streams.
/// Handles payloads split across reads via an internal buffer.
/// </summary>
public sealed class DeepSeekSseParser
{
    private readonly StringBuilder _buffer = new();
    private bool _done;

    public bool IsDone => _done;

    public IEnumerable<AIStreamEvent> Feed(string chunk)
    {
        if (_done || string.IsNullOrEmpty(chunk))
        {
            yield break;
        }

        _buffer.Append(chunk);

        while (TryReadLine(out var line))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[5..].TrimStart();
            if (payload.Length == 0)
            {
                continue;
            }

            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                _done = true;
                yield return new AIStreamEvent(null, IsDone: true);
                yield break;
            }

            foreach (var evt in ParseDataPayload(payload))
            {
                yield return evt;
                if (evt.IsDone)
                {
                    _done = true;
                    yield break;
                }
            }
        }
    }

    public void Reset()
    {
        _buffer.Clear();
        _done = false;
    }

    private bool TryReadLine(out string line)
    {
        var text = _buffer.ToString();
        var idx = text.IndexOf('\n');
        if (idx < 0)
        {
            line = string.Empty;
            return false;
        }

        line = text[..idx].TrimEnd('\r');
        _buffer.Remove(0, idx + 1);
        return true;
    }

    private static IEnumerable<AIStreamEvent> ParseDataPayload(string payload)
    {
        string? delta = null;
        string? requestId = null;
        AIUsage? usage = null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                requestId = idProp.GetString();
            }

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var deltaObj))
                {
                    if (deltaObj.TryGetProperty("content", out var contentProp) &&
                        contentProp.ValueKind == JsonValueKind.String)
                    {
                        delta = contentProp.GetString();
                    }
                    // Ignore reasoning_content for chat reply speed.
                }
            }

            if (root.TryGetProperty("usage", out var usageObj) && usageObj.ValueKind == JsonValueKind.Object)
            {
                usage = new AIUsage
                {
                    PromptTokens = usageObj.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0,
                    CompletionTokens = usageObj.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0,
                    TotalTokens = usageObj.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : 0
                };
            }
        }
        catch (JsonException)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(delta) || usage is not null)
        {
            yield return new AIStreamEvent(delta, IsDone: false, Usage: usage, RequestId: requestId);
        }
    }
}

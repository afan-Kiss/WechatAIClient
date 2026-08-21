namespace WechatAIClient.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
    Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
}

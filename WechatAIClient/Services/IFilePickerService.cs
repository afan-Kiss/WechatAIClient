namespace WechatAIClient.Services;

public interface IFilePickerService
{
    Task<string?> PickImageAsync(CancellationToken cancellationToken = default);
    Task<string?> PickFileAsync(CancellationToken cancellationToken = default);
}

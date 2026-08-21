namespace WechatAIClient.Services;

public interface IToastService
{
    string Message { get; }
    bool IsVisible { get; }
    event EventHandler? Changed;
    Task ShowAsync(string message, int durationMs = 2200);
}

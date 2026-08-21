using CommunityToolkit.Mvvm.ComponentModel;

namespace WechatAIClient.Services;

public sealed partial class ToastService : ObservableObject, IToastService
{
    private CancellationTokenSource? _hideCts;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isVisible;

    public event EventHandler? Changed;

    public async Task ShowAsync(string message, int durationMs = 2200)
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        var cts = new CancellationTokenSource();
        _hideCts = cts;

        Message = message ?? string.Empty;
        IsVisible = true;
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            await Task.Delay(Math.Max(400, durationMs), cts.Token);
            if (!cts.IsCancellationRequested)
            {
                IsVisible = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer toast
        }
    }

    partial void OnMessageChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    partial void OnIsVisibleChanged(bool value) => Changed?.Invoke(this, EventArgs.Empty);
}

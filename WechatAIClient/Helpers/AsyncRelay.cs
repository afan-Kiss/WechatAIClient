using Microsoft.Extensions.Logging;

namespace WechatAIClient.Helpers;

public static class AsyncRelay
{
    public static async void SafeFireAndForget(
        this Task task,
        ILogger? logger = null,
        Action<Exception>? onError = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected for cancelled loads / generations
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Fire-and-forget task failed");
            onError?.Invoke(ex);
        }
    }

    public static void SafeFireAndForget(
        this ValueTask task,
        ILogger? logger = null,
        Action<Exception>? onError = null)
        => task.AsTask().SafeFireAndForget(logger, onError);
}

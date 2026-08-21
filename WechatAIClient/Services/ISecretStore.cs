namespace WechatAIClient.Services;

public interface ISecretStore
{
    Task SetSecretAsync(string key, string plaintext, CancellationToken cancellationToken = default);
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
}

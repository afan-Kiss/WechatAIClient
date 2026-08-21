namespace WechatAIClient.Services.Wechat;

public enum ProfileValidationErrorCode
{
    PortConflict,
    InvalidBaseUrl,
    DuplicateProfileId
}

public sealed class ProfileValidationException : InvalidOperationException
{
    public ProfileValidationException(ProfileValidationErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public ProfileValidationErrorCode Code { get; }
}

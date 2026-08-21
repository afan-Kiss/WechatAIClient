using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAIContextBuilder
{
    AIContextBuildResult Build(AIContextBuildInput input);
}

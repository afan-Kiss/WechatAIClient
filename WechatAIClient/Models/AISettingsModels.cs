namespace WechatAIClient.Models;

public sealed class AIGlobalSettings
{
    public AIReplyMode ReplyMode { get; set; } = AIReplyMode.Auto;
    public int ContextCount { get; set; } = 10;
    public bool IncludeOwnMessages { get; set; } = true;
    public ReplyStyle ReplyStyle { get; set; } = ReplyStyle.Natural;
    public ReplyLength ReplyLength { get; set; } = ReplyLength.Medium;
    public bool AutoGenerateOnReceive { get; set; } = true;
    public GroupTriggerMode GroupTriggerMode { get; set; } = GroupTriggerMode.MentionOrQuoteMe;
}

public sealed class AIContactOverride
{
    public string ContactId { get; set; } = "";
    public bool UseOverride { get; set; }
    public AIReplyMode? ReplyMode { get; set; }
    public int? ContextCount { get; set; }
    public bool? IncludeOwnMessages { get; set; }
    public ReplyStyle? ReplyStyle { get; set; }
    public ReplyLength? ReplyLength { get; set; }
    public bool? AutoGenerateOnReceive { get; set; }
    public GroupTriggerMode? GroupTriggerMode { get; set; }
}

public sealed class EffectiveAISettings
{
    public string ContactId { get; set; } = "";
    public bool IsUsingOverride { get; set; }
    public AIReplyMode ReplyMode { get; set; }
    public int ContextCount { get; set; }
    public bool IncludeOwnMessages { get; set; }
    public ReplyStyle ReplyStyle { get; set; }
    public ReplyLength ReplyLength { get; set; }
    public bool AutoGenerateOnReceive { get; set; }
    public GroupTriggerMode GroupTriggerMode { get; set; }
}

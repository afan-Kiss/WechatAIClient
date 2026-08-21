using WechatAIClient.Models;
using WechatAIClient.Services.DeepSeek;

namespace WechatAIClient.Tests;

public class DeepSeekSseParserTests
{
    [Fact]
    public void Parses_Content_Deltas_And_Done()
    {
        var parser = new DeepSeekSseParser();
        var events = parser.Feed(
            """
            data: {"id":"req1","choices":[{"delta":{"content":"你好"}}]}

            data: {"id":"req1","choices":[{"delta":{"content":"世界"}}]}

            data: [DONE]

            """).ToList();

        Assert.Equal(3, events.Count);
        Assert.Equal("你好", events[0].DeltaContent);
        Assert.Equal("世界", events[1].DeltaContent);
        Assert.True(events[2].IsDone);
        Assert.True(parser.IsDone);
    }

    [Fact]
    public void Handles_Split_Across_Reads()
    {
        var parser = new DeepSeekSseParser();
        var part1 = parser.Feed("data: {\"choices\":[{\"delta\":{\"content\":\"A\"}}]}\n").ToList();
        var part2 = parser.Feed("data: [DO").ToList();
        var part3 = parser.Feed("NE]\n").ToList();

        Assert.Single(part1);
        Assert.Equal("A", part1[0].DeltaContent);
        Assert.Empty(part2);
        Assert.Single(part3);
        Assert.True(part3[0].IsDone);
    }

    [Fact]
    public void Ignores_Reasoning_Content()
    {
        var parser = new DeepSeekSseParser();
        var events = parser.Feed(
            """
            data: {"choices":[{"delta":{"reasoning_content":"think","content":"ok"}}]}

            data: [DONE]

            """).ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal("ok", events[0].DeltaContent);
        Assert.True(events[1].IsDone);
    }
}

using Bunit;
using m1Chat.Client.Components;
using System.IO;
using System.Linq;
using Xunit;

namespace m1Chat.Client.Tests;

public sealed class AiMarkdownTests : TestContext
{
    [Fact]
    public void ReRenders_OnEveryStreamingMarkdownUpdate()
    {
        var jsCall = JSInterop.SetupVoid("setInnerHtmlAndRenderMath", _ => true);

        var cut = RenderComponent<AiMarkdown>(parameters => parameters
            .Add(p => p.Markdown, "Hello")
            .Add(p => p.IsStreaming, true));

        Assert.Single(jsCall.Invocations);

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Markdown, "Hello **world**")
            .Add(p => p.IsStreaming, true));

        Assert.Equal(2, jsCall.Invocations.Count);
        var secondCall = jsCall.Invocations.Last();
        Assert.Contains("<strong>world</strong>", secondCall.Arguments[1]?.ToString());
    }

    [Fact]
    public void StreamingPreviewNormalization_ClosesOpenFenceAndMath()
    {
        const string partial = "$$x + 1\n\nInline $y\n\n```csharp\nvar n = 1;";

        var normalized = AiMarkdown.PrepareMarkdownForRender(partial, isStreaming: true);

        Assert.EndsWith("$\n$$\n```", normalized.Replace("\r", string.Empty));
    }

    [Fact]
    public void FinalRender_UsesRawMarkdownWithoutStreamingNormalization()
    {
        const string partial = "```csharp\nvar n = 1;\n\n$$x + 1\n\nInline $y";

        var final = AiMarkdown.PrepareMarkdownForRender(partial, isStreaming: false);

        Assert.Equal(partial, final);
    }

    [Fact]
    public void MainLayout_ConfiguresBothInlineAndDisplayMathDelimiters()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var mainLayoutPath = Path.Combine(root, "m1Chat.Client", "Layout", "MainLayout.razor");

        Assert.True(File.Exists(mainLayoutPath), $"Missing file: {mainLayoutPath}");
        var fileContents = File.ReadAllText(mainLayoutPath);

        Assert.Contains("{left: \"$$\", right: \"$$\", display: true}", fileContents);
        Assert.Contains("{left: \"$\", right: \"$\", display: false}", fileContents);
    }

    [Fact]
    public void StreamingPartialMarkdown_DoesNotThrowDuringRender()
    {
        var jsCall = JSInterop.SetupVoid("setInnerHtmlAndRenderMath", _ => true);

        var exception = Record.Exception(() =>
            RenderComponent<AiMarkdown>(parameters => parameters
                .Add(p => p.Markdown, "```python\nprint('hello')\n\n$unfinished")
                .Add(p => p.IsStreaming, true)));

        Assert.Null(exception);
        Assert.Single(jsCall.Invocations);
    }
}

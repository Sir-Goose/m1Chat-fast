using Bunit;
using m1Chat.Client.Components;
using m1Chat.Client.Models;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace m1Chat.Client.Tests;

public sealed class ChatMessageItemStreamingTests : TestContext
{
    public ChatMessageItemStreamingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void ChatMessageItem_ProgressivelyRendersFormattedMarkdownDuringStreaming()
    {
        var jsCall = JSInterop.SetupVoid("setInnerHtmlAndRenderMath", _ => true);
        var message = new ClientChatMessage
        {
            IsUser = false,
            IsStreaming = true,
            Text = ""
        };

        RenderComponent<MudPopoverProvider>();

        var cut = RenderComponent<ChatMessageItem>(parameters => parameters
            .Add(p => p.Message, message));

        message.Text = "- one\n";
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Message, message));

        var htmlAfterFirstChunk = jsCall.Invocations.Last().Arguments[1]?.ToString() ?? string.Empty;
        Assert.Contains("<ul>", htmlAfterFirstChunk);
        Assert.Contains("<li>one</li>", htmlAfterFirstChunk);

        message.Text += "- two";
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Message, message));

        var htmlAfterSecondChunk = jsCall.Invocations.Last().Arguments[1]?.ToString() ?? string.Empty;
        Assert.Contains("<li>one</li>", htmlAfterSecondChunk);
        Assert.Contains("<li>two</li>", htmlAfterSecondChunk);
    }
}

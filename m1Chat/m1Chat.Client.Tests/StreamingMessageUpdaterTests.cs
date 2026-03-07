using m1Chat.Client.Models;
using m1Chat.Client.Services;
using Xunit;

namespace m1Chat.Client.Tests;

public sealed class StreamingMessageUpdaterTests
{
    [Fact]
    public void ApplyChunk_AppendsIncrementally()
    {
        var message = new ClientChatMessage { IsUser = false, Text = string.Empty, IsStreaming = true };
        var updater = new StreamingMessageUpdater(message);

        updater.ApplyChunk("Hello");
        updater.ApplyChunk(" world");

        Assert.Equal("Hello world", message.Text);
    }

    [Fact]
    public void Complete_SetsStreamingFalse_WithoutDroppingText()
    {
        var message = new ClientChatMessage { IsUser = false, Text = "Partial", IsStreaming = true };
        var updater = new StreamingMessageUpdater(message);

        updater.ApplyChunk(" content");
        updater.Complete();

        Assert.Equal("Partial content", message.Text);
        Assert.False(message.IsStreaming);
    }

    [Fact]
    public void AppendError_PreservesTextAndAppendsErrorMarker()
    {
        var message = new ClientChatMessage { IsUser = false, Text = "Already streamed.", IsStreaming = true };
        var updater = new StreamingMessageUpdater(message);

        updater.AppendError("network failed");

        Assert.Equal("Already streamed.[Error: network failed]", message.Text);
        Assert.False(message.IsStreaming);
    }
}

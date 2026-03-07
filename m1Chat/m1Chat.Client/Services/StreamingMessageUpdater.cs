using m1Chat.Client.Models;

namespace m1Chat.Client.Services;

public sealed class StreamingMessageUpdater
{
    private readonly ClientChatMessage _message;

    public StreamingMessageUpdater(ClientChatMessage message)
    {
        _message = message;
    }

    public void ApplyChunk(string? chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        _message.Text += chunk;
    }

    public void Complete()
    {
        _message.IsStreaming = false;
    }

    public void AppendError(string error)
    {
        _message.Text += $"[Error: {error}]";
        _message.IsStreaming = false;
    }
}

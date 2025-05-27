// File: m1Chat.Client.Services/ChatCompletionService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace m1Chat.Client.Services
{
    public class ChatMessage
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
        public List<Guid>? FileIds { get; set; }
    }

    public class ChatCompletionService
    {
        private readonly IJSRuntime _jsRuntime;

        public ChatCompletionService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public IAsyncEnumerable<string> StreamCompletionAsync(
            List<ChatMessage> messages,
            string model,
            string reasoningEffort
        )
        {
            var channel = Channel.CreateUnbounded<string>();
            var writer = channel.Writer;
            var dotnetRef = DotNetObjectReference.Create(
                new StreamCallback(writer)
            );

            var payload = new
            {
                model,
                reasoningEffort,
                messages = messages
                    .Select(m => new { role = m.Role, content = m.Content, fileIds = m.FileIds })
                    .ToArray()
            };

            _ = _jsRuntime.InvokeVoidAsync(
                "streamChatCompletion",
                "/api/completions/stream",
                payload,
                dotnetRef
            );

            return channel.Reader.ReadAllAsync();
        }

        private class StreamCallback
        {
            private readonly ChannelWriter<string> _writer;

            public StreamCallback(ChannelWriter<string> writer) =>
                _writer = writer;

            [JSInvokable("Receive")]
            public void Receive(string chunk)
            {
                _writer.TryWrite(chunk);
            }

            [JSInvokable("Complete")]
            public void Complete()
            {
                _writer.TryComplete();
            }

            [JSInvokable("Error")]
            public void Error(string errorMessage)
            {
                _writer.TryComplete(new Exception(errorMessage));
            }
        }
    }
}